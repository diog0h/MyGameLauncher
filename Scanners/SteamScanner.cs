using GameLauncher.Localization;
using GameLauncher.Models;
using Microsoft.Win32;

namespace GameLauncher.Scanners;

/// <summary>
/// Encontra os jogos Steam instalados lendo os ficheiros da própria Steam:
/// Registo → pasta da Steam → libraryfolders.vdf → appmanifest_*.acf.
/// O tempo de jogo vem de userdata\&lt;conta&gt;\config\localconfig.vdf.
/// </summary>
public sealed class SteamScanner
{
    /// <summary>Linhas de diagnóstico mostradas na app (útil para perceber o que falhou).</summary>
    public List<string> Log { get; } = new();

    public string? SteamRoot { get; private set; }

    // AppIDs que são ferramentas/redistribuíveis, não jogos.
    private static readonly HashSet<string> NonGameAppIds = new()
    {
        "228980",  // Steamworks Common Redistributables
        "250820",  // SteamVR
        "1070560", // Steam Linux Runtime
        "1391110", // Steam Linux Runtime - Soldier
        "1628350", // Steam Linux Runtime - Sniper
        "1493710", // Proton Experimental
        "2180100", // Proton Hotfix
        "1826330", // Proton EasyAntiCheat Runtime
        "1161040", // Proton BattlEye Runtime
    };

    private static readonly string[] NonGameNameParts =
    {
        "Redistributable", "Steamworks Common", "Proton ", "Steam Linux Runtime", "Dedicated Server",
    };

    private sealed record SteamStats(int PlaytimeMinutes, DateTime? LastPlayed);

    public List<Game> Scan()
    {
        var games = new List<Game>();

        SteamRoot = FindSteamRoot();
        if (SteamRoot is null)
        {
            Log.Add(Loc.T("Log_SteamNotFound"));
            return games;
        }
        Log.Add(Loc.F("Log_SteamFolder", SteamRoot));

        var stats = ReadUserStats(SteamRoot);
        var libraries = FindLibraries(SteamRoot);
        Log.Add(Loc.F("Log_Libraries", libraries.Count));

        var seenIds = new HashSet<string>();
        foreach (var lib in libraries)
        {
            var steamapps = Path.Combine(lib, "steamapps");
            if (!Directory.Exists(steamapps))
            {
                Log.Add(Loc.F("Log_NoSteamapps", lib));
                continue;
            }

            string[] manifests;
            try { manifests = Directory.GetFiles(steamapps, "appmanifest_*.acf"); }
            catch (Exception ex) { Log.Add(Loc.F("Log_ReadError", lib, ex.Message)); continue; }

            int countHere = 0;
            foreach (var file in manifests)
            {
                var game = ReadManifest(file, steamapps, stats);
                if (game is null || !seenIds.Add(game.Id)) continue;
                games.Add(game);
                countHere++;
            }
            Log.Add(Loc.F("Log_LibGames", lib, countHere));
        }

        return games;
    }

    private string? FindSteamRoot()
    {
        var candidates = new List<string?>
        {
            Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string,
            Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string,
            Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string,
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
        };

        foreach (var c in candidates)
        {
            if (string.IsNullOrWhiteSpace(c)) continue;
            try
            {
                var full = Path.GetFullPath(c);
                if (Directory.Exists(Path.Combine(full, "steamapps"))) return full;
            }
            catch { /* caminho inválido: tenta o seguinte */ }
        }
        return null;
    }

    private List<string> FindLibraries(string steamRoot)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? p)
        {
            if (string.IsNullOrWhiteSpace(p)) return;
            try
            {
                var full = Path.GetFullPath(p).TrimEnd('\\', '/');
                if (seen.Add(full)) result.Add(full);
            }
            catch { }
        }

        Add(steamRoot);

        var vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdfPath))
        {
            Log.Add(Loc.T("Log_NoLibraryVdf"));
            return result;
        }

        try
        {
            var root = VdfParser.ParseFile(vdfPath);
            var folders = root.Child("libraryfolders");
            if (folders is null) return result;

            foreach (var (_, value) in folders)
            {
                // Formato novo: "0" { "path" "D:\\SteamLibrary" ... }
                if (value is VdfParser.Node n) Add(n.Str("path"));
                // Formato antigo: "1" "D:\\SteamLibrary"
                else if (value is string s && (s.Contains('\\') || s.Contains('/'))) Add(s);
            }
        }
        catch (Exception ex)
        {
            Log.Add(Loc.F("Log_LibraryVdfError", ex.Message));
        }

        return result;
    }

    /// <summary>Lê o tempo de jogo da conta Steam usada mais recentemente neste PC.</summary>
    private Dictionary<string, SteamStats> ReadUserStats(string steamRoot)
    {
        var result = new Dictionary<string, SteamStats>();
        try
        {
            var userdata = Path.Combine(steamRoot, "userdata");
            if (!Directory.Exists(userdata)) return result;

            var config = Directory.GetDirectories(userdata)
                .Select(d => Path.Combine(d, "config", "localconfig.vdf"))
                .Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (config is null) { Log.Add(Loc.T("Log_NoLocalConfig")); return result; }

            var apps = VdfParser.ParseFile(config)
                .Child("UserLocalConfigStore")?.Child("Software")?.Child("Valve")?.Child("Steam")?.Child("apps");
            if (apps is null) return result;

            foreach (var (id, value) in apps)
            {
                if (value is not VdfParser.Node n) continue;
                int.TryParse(n.Str("Playtime"), out var minutes);
                DateTime? last = null;
                if (long.TryParse(n.Str("LastPlayed"), out var unix) && unix > 0)
                    last = DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;
                result[id] = new SteamStats(minutes, last);
            }
            Log.Add(Loc.F("Log_PlaytimeRead", Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(config))) ?? "?", result.Count));
        }
        catch (Exception ex)
        {
            Log.Add(Loc.F("Log_PlaytimeError", ex.Message));
        }
        return result;
    }

    private Game? ReadManifest(string file, string steamapps, Dictionary<string, SteamStats> stats)
    {
        try
        {
            var app = VdfParser.ParseFile(file).Child("AppState");
            if (app is null) return null;

            var id = app.Str("appid");
            var name = app.Str("name");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) return null;

            if (NonGameAppIds.Contains(id)) return null;
            if (NonGameNameParts.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase))) return null;

            var installDir = app.Str("installdir");
            long.TryParse(app.Str("SizeOnDisk"), out var size);
            int.TryParse(app.Str("StateFlags"), out var flags);

            DateTime? lastPlayed = null;
            if (long.TryParse(app.Str("LastPlayed"), out var unix) && unix > 0)
                lastPlayed = DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;

            stats.TryGetValue(id, out var s);
            if (s?.LastPlayed is { } sl && (lastPlayed is null || sl > lastPlayed)) lastPlayed = sl;

            return new Game
            {
                Id = id,
                Name = name,
                Store = "Steam",
                InstallPath = string.IsNullOrEmpty(installDir) ? null : Path.Combine(steamapps, "common", installDir),
                SizeBytes = size,
                FullyInstalled = (flags & 4) == 4, // 4 = totalmente instalado
                LaunchUri = $"steam://rungameid/{id}",
                StoreDetailsUri = $"steam://nav/games/details/{id}",
                SteamAppId = id,
                SteamLastPlayed = lastPlayed,
                StorePlaytimeMinutes = s?.PlaytimeMinutes ?? 0,
            };
        }
        catch (Exception ex)
        {
            Log.Add(Loc.F("Log_ManifestError", Path.GetFileName(file), ex.Message));
            return null;
        }
    }
}
