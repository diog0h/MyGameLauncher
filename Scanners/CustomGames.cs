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
            var folder = GameFolder(c);

            games.Add(new Game
            {
                Id = c.Id,
                Name = c.Name,
                Store = c.Store,
                InstallPath = folder,
                SizeBytes = c.SizeFolder is not null && string.Equals(c.SizeFolder, folder, StringComparison.OrdinalIgnoreCase) ? c.SizeBytes : 0,
                LaunchUri = c.Path,
                LaunchArguments = string.IsNullOrWhiteSpace(c.Arguments) ? null : c.Arguments,
                FullyInstalled = File.Exists(c.Path) || c.Path.Contains("://"),
                IsCustom = true,
            });
        }
        return games;
    }

    /// <summary>Pasta do jogo: a escolhida pelo utilizador, ou a do .exe (também através de um atalho).</summary>
    public static string? GameFolder(CustomGame c)
    {
        if (!string.IsNullOrWhiteSpace(c.Folder)) return c.Folder.TrimEnd('\\', '/');
        var target = ResolveTarget(c.Path);
        return target is not null && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(target) : null;
    }

    /// <summary>Soma o tamanho de todos os ficheiros da pasta (pode demorar em pastas grandes: usar em segundo plano).</summary>
    public static long FolderSize(string folder)
    {
        try
        {
            var opts = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            return new DirectoryInfo(folder).EnumerateFiles("*", opts).Sum(f => f.Length);
        }
        catch { return 0; }
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
