using System.Text.Json;
using GameLauncher.Localization;
using GameLauncher.Models;
using Microsoft.Win32;

namespace GameLauncher.Scanners;

/// <summary>
/// Jogos instalados pela Epic Games: cada um tem um ficheiro .item (JSON) na pasta Manifests do launcher.
/// </summary>
public sealed class EpicScanner
{
    public const string StoreName = "Epic Games";
    public List<string> Log { get; } = new();

    public List<Game> Scan()
    {
        var games = new List<Game>();
        var dir = FindManifestsFolder();
        if (dir is null)
        {
            Log.Add(Loc.T("Log_EpicNotFound"));
            return games;
        }

        foreach (var file in SafeFiles(dir, "*.item"))
        {
            try
            {
                if (ParseManifest(File.ReadAllText(file), Directory.Exists) is { } game) games.Add(game);
            }
            catch (Exception ex)
            {
                Log.Add(Loc.F("Log_ManifestError", Path.GetFileName(file), ex.Message));
            }
        }

        Log.Add(Loc.F("Log_StoreGames", StoreName, games.Count));
        return games;
    }

    /// <summary>Lê um manifesto .item. Devolve null para DLC, motores/plugins e jogos sem pasta.</summary>
    public static Game? ParseManifest(string json, Func<string, bool> folderExists)
    {
        using var doc = JsonDocument.Parse(json);
        var j = doc.RootElement;

        string? Str(string name) => j.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        bool Bool(string name) => j.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

        var name = Str("DisplayName");
        var appName = Str("AppName");
        var ns = Str("CatalogNamespace");
        var itemId = Str("CatalogItemId");
        var folder = Str("InstallLocation");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(appName) || folder is null) return null;

        // DLC e componentes: têm um "jogo principal" diferente deles próprios.
        var main = Str("MainGameAppName");
        if (!string.IsNullOrEmpty(main) && !main.Equals(appName, StringComparison.OrdinalIgnoreCase)) return null;

        // Unreal Engine, plugins, etc. não são jogos.
        if (j.TryGetProperty("AppCategories", out var cats) && cats.ValueKind == JsonValueKind.Array)
        {
            var list = cats.EnumerateArray().Select(c => c.GetString() ?? "").ToList();
            if (list.Any(c => c is "engines" or "plugins" or "digitalextras")) return null;
        }

        if (!folderExists(folder)) return null;
        long size = j.TryGetProperty("InstallSize", out var sz) && sz.TryGetInt64(out var n) ? n : 0;

        return new Game
        {
            Id = "epic:" + appName,
            Name = name,
            Store = StoreName,
            InstallPath = folder,
            SizeBytes = size,
            FullyInstalled = !Bool("bIsIncompleteInstall"),
            LaunchUri = $"com.epicgames.launcher://apps/{Uri.EscapeDataString($"{ns}:{itemId}:{appName}")}?action=launch&silent=true",
            StoreDetailsUri = "com.epicgames.launcher://library",
        };
    }

    private static string? FindManifestsFolder()
    {
        var candidates = new List<string?>();
        if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Epic Games\EpicGamesLauncher", "AppDataPath", null) is string appData)
            candidates.Add(Path.Combine(appData, "Manifests"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests"));

        return candidates.FirstOrDefault(c => c is not null && Directory.Exists(c));
    }

    private static IEnumerable<string> SafeFiles(string dir, string pattern)
    {
        try { return Directory.GetFiles(dir, pattern); } catch { return Array.Empty<string>(); }
    }
}
