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
    /// <summary>(jogo, a correr?) — chamado na thread de fundo.</summary>
    public event Action<Game, bool>? RunningChanged;

    private readonly Dictionary<string, List<(Game Game, string Folder)>> _exeIndex =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _running = new();
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
            lock (_exeIndex) _running.Clear();
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

    private void Tick()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        try
        {
            var nowRunning = new HashSet<Game>();
            Process[] procs;
            try { procs = Process.GetProcesses(); } catch { return; }

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
                                if (IsInside(path, o.Folder)) nowRunning.Add(o.Game);
                        }
                        else if (owners.Count == 1 && !GenericNames.Contains(p.ProcessName))
                        {
                            // O Windows não deixou ler o caminho: confia no nome, se for único e não genérico.
                            nowRunning.Add(owners[0].Game);
                        }
                    }
                    catch { }
                    finally { p.Dispose(); }
                }
            }

            lock (_exeIndex)
            {
                foreach (var g in nowRunning)
                    if (_running.Add(g.Id)) RunningChanged?.Invoke(g, true);

                foreach (var id in _running.ToList())
                {
                    if (nowRunning.Any(g => g.Id == id)) continue;
                    _running.Remove(id);
                    var g = _exeIndex.Values.SelectMany(v => v).Select(v => v.Game).FirstOrDefault(x => x.Id == id);
                    if (g is not null) RunningChanged?.Invoke(g, false);
                }
            }
        }
        finally { Interlocked.Exchange(ref _busy, 0); }
    }
}
