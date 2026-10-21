using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using GameLauncher.Models;

namespace GameLauncher.Services;

/// <summary>
/// Vigia os processos do Windows e percebe quando um jogo está aberto, comparando
/// os executáveis em execução com os .exe que existem na pasta de cada jogo.
/// Funciona com jogos abertos por esta app ou diretamente pela Steam.
/// Para não contar tempo a programas que só têm o mesmo nome, confirma sempre que o processo
/// está mesmo dentro da pasta do jogo; só usa o nome sozinho quando o Windows não deixa ler o caminho
/// (ex.: jogos com anti-batota) e o nome não é genérico.
/// </summary>
public sealed class PlaytimeTracker : IDisposable
{
    /// <summary>(jogo, a correr?, quando começou/acabou) — chamado na thread de fundo.</summary>
    public event Action<Game, bool, DateTime>? RunningChanged;

    /// <summary>(jogo, preso?) — o processo continua aberto mas sem janela há algum tempo (ex.: o jogo não fechou bem).</summary>
    public event Action<Game, bool>? StuckChanged;

    /// <summary>Sem nenhuma janela durante este tempo, o jogo conta como "preso" e o tempo deixa de contar.</summary>
    private static readonly TimeSpan StuckAfter = TimeSpan.FromMinutes(3);

    private sealed class RunInfo
    {
        public DateTime Started;
        public DateTime LastWindow;
        public bool Stuck;
        public List<int> ProcessIds = new();
    }

    private readonly Dictionary<string, List<(Game Game, string Folder)>> _exeIndex =
        new(StringComparer.OrdinalIgnoreCase);
    // Por id do jogo (não pelo objeto): sobrevive a "Atualizar biblioteca", que cria jogos novos.
    private readonly Dictionary<string, RunInfo> _running = new();
    private Timer? _timer;
    private int _busy;

    // Executáveis que existem nas pastas dos jogos mas não são o jogo.
    private static readonly string[] IgnoredExeParts =
    {
        "unins", "setup", "install", "redist", "vcredist", "vc_redist", "dxsetup", "dotnet", "crash",
        "report", "easyanticheat", "battleye", "beservice", "updater", "uploader", "cefprocess",
        "unitycrashhandler", "ue4prereq", "prereq", "helper", "touchup", "cleanup",
    };

    // Nomes demasiado comuns para identificar um jogo só pelo nome.
    private static readonly HashSet<string> GenericNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "launcher", "game", "start", "play", "client", "bootstrap", "bootstrapper", "main", "app",
        "run", "server", "editor", "config", "settings", "launch", "update", "x64", "win64", "shipping",
    };

    private int _generation;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder exeName, ref int size);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    private const uint ProcessQueryLimitedInformation = 0x1000;

    /// <summary>
    /// Caminho completo do executável. Usa o acesso "limitado" do Windows, que funciona mesmo com
    /// muitos processos protegidos e com programas de 32 bits (ao contrário de Process.MainModule).
    /// </summary>
    private static string? GetProcessPath(int pid)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            return QueryFullProcessImageName(handle, 0, sb, ref size) ? sb.ToString(0, size) : null;
        }
        finally { CloseHandle(handle); }
    }

    private static bool IsInside(string path, string folder)
    {
        var f = folder.TrimEnd('\\', '/') + "\\";
        return path.StartsWith(f, StringComparison.OrdinalIgnoreCase);
    }

    public void Start(IEnumerable<Game> games)
    {
        Stop();
        var list = games.ToList();
        int gen = Interlocked.Increment(ref _generation);

        Task.Run(() =>
        {
            BuildIndex(list);
            if (gen != Volatile.Read(ref _generation)) return; // entretanto houve outra atualização
            _timer = new Timer(_ => Tick(), null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        });
    }

    public void Stop()
    {
        Interlocked.Increment(ref _generation);
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => Stop();

    private void BuildIndex(List<Game> games)
    {
        lock (_exeIndex)
        {
            _exeIndex.Clear();
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                MaxRecursionDepth = 4,
            };

            foreach (var g in games)
            {
                if (g.InstallPath is not { } folder || !Directory.Exists(folder)) continue;
                try
                {
                    foreach (var exe in Directory.EnumerateFiles(folder, "*.exe", options))
                    {
                        var name = Path.GetFileNameWithoutExtension(exe);
                        var lower = name.ToLowerInvariant();
                        if (IgnoredExeParts.Any(lower.Contains)) continue;

                        if (!_exeIndex.TryGetValue(name, out var owners))
                            _exeIndex[name] = owners = new();
                        if (!owners.Any(o => o.Game == g)) owners.Add((g, folder));
                    }
                }
                catch { }
            }
        }
    }

    /// <summary>Processos do jogo que estão abertos agora (para o botão "Fechar jogo").</summary>
    public List<int> ProcessIds(string gameId)
    {
        lock (_exeIndex) return _running.TryGetValue(gameId, out var info) ? info.ProcessIds.ToList() : new();
    }

    /// <summary>Quando o jogo deixou de ter janela, se estiver preso (null se não estiver).</summary>
    public DateTime? StuckSince(string gameId)
    {
        lock (_exeIndex) return _running.TryGetValue(gameId, out var info) && info.Stuck ? info.LastWindow : null;
    }

    public bool IsStuck(string gameId)
    {
        lock (_exeIndex) return _running.TryGetValue(gameId, out var info) && info.Stuck;
    }

    private void Tick()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        try
        {
            var found = new Dictionary<string, (Game Game, List<int> Pids, bool HasWindow)>();
            Process[] procs;
            try { procs = Process.GetProcesses(); } catch { return; }

            void Add(Game g, Process p)
            {
                bool window = false;
                try { window = p.MainWindowHandle != IntPtr.Zero; } catch { }
                if (found.TryGetValue(g.Id, out var e)) { e.Pids.Add(p.Id); found[g.Id] = (e.Game, e.Pids, e.HasWindow || window); }
                else found[g.Id] = (g, new List<int> { p.Id }, window);
            }

            lock (_exeIndex)
            {
                foreach (var p in procs)
                {
                    try
                    {
                        if (!_exeIndex.TryGetValue(p.ProcessName, out var owners)) continue;

                        var path = GetProcessPath(p.Id);
                        if (path is not null)
                        {
                            // Caminho conhecido: só conta se o .exe estiver dentro da pasta do jogo.
                            foreach (var o in owners)
                                if (IsInside(path, o.Folder)) Add(o.Game, p);
                        }
                        else if (owners.Count == 1 && !GenericNames.Contains(p.ProcessName))
                        {
                            // O Windows não deixou ler o caminho: confia no nome, se for único e não genérico.
                            Add(owners[0].Game, p);
                        }
                    }
                    catch { }
                    finally { p.Dispose(); }
                }
            }

            var now = DateTime.Now;
            var events = new List<Action>();
            lock (_exeIndex)
            {
                foreach (var (id, f) in found)
                {
                    if (!_running.TryGetValue(id, out var info))
                    {
                        info = _running[id] = new RunInfo { Started = now, LastWindow = now };
                        var started = info.Started;
                        events.Add(() => RunningChanged?.Invoke(f.Game, true, started));
                    }
                    info.ProcessIds = f.Pids;

                    if (f.HasWindow)
                    {
                        info.LastWindow = now;
                        if (info.Stuck) { info.Stuck = false; events.Add(() => StuckChanged?.Invoke(f.Game, false)); }
                    }
                    else if (!info.Stuck && now - info.LastWindow > StuckAfter)
                    {
                        // Sem janela há minutos: o jogo provavelmente fechou mal e ficou um processo pendurado.
                        info.Stuck = true;
                        events.Add(() => StuckChanged?.Invoke(f.Game, true));
                        AppLog.Info($"{f.Game.Name}: o processo continua aberto mas sem janela (pids {string.Join(", ", f.Pids)})");
                    }
                }

                foreach (var (id, info) in _running.ToList())
                {
                    if (found.ContainsKey(id)) continue;
                    _running.Remove(id);
                    var g = _exeIndex.Values.SelectMany(v => v).Select(v => v.Game).FirstOrDefault(x => x.Id == id);
                    if (g is null) continue;
                    // Se ficou pendurado, o tempo de jogo acaba quando a janela desapareceu (não quando o processo morreu).
                    var end = info.Stuck ? info.LastWindow : now;
                    events.Add(() => RunningChanged?.Invoke(g, false, end));
                    if (info.Stuck) events.Add(() => StuckChanged?.Invoke(g, false));
                }
            }
            foreach (var e in events) e();
        }
        finally { Interlocked.Exchange(ref _busy, 0); }
    }
}
