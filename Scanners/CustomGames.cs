using GameLauncher.Models;

namespace GameLauncher.Scanners;

/// <summary>Transforma os jogos adicionados à mão em fichas de jogo.</summary>
public static class CustomGames
{
    public static List<Game> Load(IEnumerable<CustomGame> items)
    {
        var games = new List<Game>();
        foreach (var c in items)
        {
            var target = ResolveTarget(c.Path);
            var folder = target is not null && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? Path.GetDirectoryName(target) : null;

            games.Add(new Game
            {
                Id = c.Id,
                Name = c.Name,
                Store = c.Store,
                InstallPath = folder,
                LaunchUri = c.Path,
                LaunchArguments = string.IsNullOrWhiteSpace(c.Arguments) ? null : c.Arguments,
                FullyInstalled = File.Exists(c.Path) || c.Path.Contains("://"),
                IsCustom = true,
            });
        }
        return games;
    }

    /// <summary>Para um atalho (.lnk) devolve o programa para onde aponta; para um .exe devolve o próprio.</summary>
    public static string? ResolveTarget(string path)
    {
        if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return path;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return null;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic link = shell.CreateShortcut(path);
            string target = link.TargetPath;
            return string.IsNullOrEmpty(target) ? null : target;
        }
        catch { return null; }
    }
}
