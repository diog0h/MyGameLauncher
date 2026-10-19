using GameLauncher.Localization;
using GameLauncher.Models;
using Microsoft.Win32;

namespace GameLauncher.Scanners;

/// <summary>
/// Jogos instalados pelo Ubisoft Connect: estão no Registo, em Ubisoft\Launcher\Installs\&lt;id&gt;.
/// O nome vem da entrada de desinstalação do Windows ("Uplay Install &lt;id&gt;").
/// </summary>
public sealed class UbisoftScanner
{
    public const string StoreName = "Ubisoft Connect";
    public List<string> Log { get; } = new();

    public List<Game> Scan()
    {
        var games = new List<Game>();
        using var installs = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs")
                             ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Ubisoft\Launcher\Installs");
        if (installs is null)
        {
            Log.Add(Loc.T("Log_UbisoftNotFound"));
            return games;
        }

        foreach (var id in installs.GetSubKeyNames())
        {
            try
            {
                using var key = installs.OpenSubKey(id);
                var folder = (key?.GetValue("InstallDir") as string)?.Replace('/', '\\').TrimEnd('\\');
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) continue;

                var (name, sizeKb) = ReadUninstallInfo(id);
                name ??= Path.GetFileName(folder);

                games.Add(new Game
                {
                    Id = "uplay:" + id,
                    Name = name,
                    Store = StoreName,
                    InstallPath = folder,
                    SizeBytes = sizeKb > 0 ? sizeKb * 1024 : FolderSize(folder),
                    LaunchUri = $"uplay://launch/{id}/0",
                    StoreDetailsUri = "uplay://",
                });
            }
            catch (Exception ex)
            {
                Log.Add(Loc.F("Log_ManifestError", id, ex.Message));
            }
        }

        Log.Add(Loc.F("Log_StoreGames", StoreName, games.Count));
        return games;
    }

    private static (string? Name, long SizeKb) ReadUninstallInfo(string id)
    {
        foreach (var root in new[]
                 {
                     @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Uplay Install " + id,
                     @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Uplay Install " + id,
                 })
        {
            using var k = Registry.LocalMachine.OpenSubKey(root);
            if (k?.GetValue("DisplayName") is string name && name.Length > 0)
            {
                long size = k.GetValue("EstimatedSize") is int kb ? kb : 0;
                return (name, size);
            }
        }
        return (null, 0);
    }

    private static long FolderSize(string folder)
    {
        try
        {
            var opts = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
            return new DirectoryInfo(folder).EnumerateFiles("*", opts).Sum(f => f.Length);
        }
        catch { return 0; }
    }
}
