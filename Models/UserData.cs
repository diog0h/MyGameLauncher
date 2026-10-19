namespace GameLauncher.Models;

/// <summary>Tudo o que a app guarda no PC (em %LocalAppData%\GameLauncher\dados.json).</summary>
public sealed class UserData
{
    public HashSet<string> Favorites { get; set; } = new();
    public List<GameCollection> Collections { get; set; } = new();
    public List<PlaySession> Sessions { get; set; } = new();
    public List<CustomGame> CustomGames { get; set; } = new();

    /// <summary>Cor de destaque calculada por jogo (hex), para não recalcular sempre.</summary>
    public Dictionary<string, string> Colors { get; set; } = new();

    public AppSettings Settings { get; set; } = new();
}

public sealed class GameCollection
{
    public string Name { get; set; } = "";
    public List<string> GameIds { get; set; } = new();
}

/// <summary>Jogo adicionado à mão (qualquer .exe ou atalho), com o nome de loja/launcher que o utilizador escolheu.</summary>
public sealed class CustomGame
{
    public string Id { get; set; } = "custom:" + Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Store { get; set; } = "";
    public string Path { get; set; } = "";
    public string? Arguments { get; set; }
}

public sealed class PlaySession
{
    public string GameId { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public double Seconds => Math.Max(0, (End - Start).TotalSeconds);
}

public sealed class AppSettings
{
    public string? SteamGridDbKey { get; set; }
    public bool StartFullscreen { get; set; }
    public string LibrarySort { get; set; } = "recent";

    /// <summary>Código da língua (pt, en, es, fr, de). Vazio = a do Windows.</summary>
    public string? Language { get; set; }

    /// <summary>Cor de fundo da app (hex). Vazio = cinzento-escuro por defeito.</summary>
    public string? ThemeColor { get; set; }

    /// <summary>No Início e na página do jogo, usar a cor da capa do jogo.</summary>
    public bool UseGameColors { get; set; } = true;

    /// <summary>Tamanho e posição da janela da última vez (null = ainda não há).</summary>
    public WindowPlacement? Window { get; set; }
}

public sealed class WindowPlacement
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Maximized { get; set; }
}
