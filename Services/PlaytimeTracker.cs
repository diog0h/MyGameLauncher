using System.Diagnostics;
using GameLauncher.Models;

namespace GameLauncher.Services;

/// <summary>
/// Vigia os processos do Windows e percebe quando um jogo está aberto, comparando
/// os executáveis em execução com os .exe que existem na pasta de cada jogo.
/// Funciona com jogos abertos por esta app ou diretamente pela Steam.
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

    private int _generation;

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

                        if (owners.Count == 1)
                        {
                            nowRunning.Add(owners[0].Game);
                            continue;
                        }

                        // Nome repetido em vários jogos (ex.: "launcher"): confirma pela pasta.
                        string? path = null;
                        try { path = p.MainModule?.FileName; } catch { }
                        if (path is null) continue;
                        foreach (var o in owners)
                            if (path.StartsWith(o.Folder, StringComparison.OrdinalIgnoreCase))
                                nowRunning.Add(o.Game);
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
