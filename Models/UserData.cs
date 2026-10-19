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

    /// <summary>Imagens escolhidas à mão, por jogo (substituem as encontradas automaticamente).</summary>
    public Dictionary<string, CustomArtwork> CustomArt { get; set; } = new();

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

/// <summary>Caminhos das imagens escolhidas pelo utilizador (cópias guardadas na pasta "art" da app).</summary>
public sealed class CustomArtwork
{
    public string? Cover { get; set; }
    public string? Hero { get; set; }
    public string? Logo { get; set; }
    public string? Header { get; set; }

    public bool IsEmpty => Cover is null && Hero is null && Logo is null && Header is null;
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
    /// <summary>Chave do SteamGridDB, cifrada com a DPAPI do Windows (ver <see cref="GameLauncher.Services.Secrets"/>).</summary>
    public string? SteamGridDbKeyProtected { get; set; }

    /// <summary>Versões até à 1.0.2 guardavam a chave em texto simples aqui. Só é lida (para migrar) e depois apagada.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("SteamGridDbKey")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacySteamGridDbKey { get; set; }

    private string? _keyCache;

    /// <summary>A chave já decifrada (não é gravada assim no ficheiro).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? SteamGridDbKey
    {
        get => _keyCache ??= GameLauncher.Services.Secrets.Unprotect(SteamGridDbKeyProtected);
        set
        {
            _keyCache = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            SteamGridDbKeyProtected = GameLauncher.Services.Secrets.Protect(_keyCache);
            // Se a cifra falhar (raro), não perde a chave: fica como estava.
            LegacySteamGridDbKey = SteamGridDbKeyProtected is null ? _keyCache : null;
        }
    }

    /// <summary>Converte a chave antiga (texto simples) para a versão cifrada. Devolve true se mudou algo.</summary>
    public bool MigrateSecrets()
    {
        if (string.IsNullOrWhiteSpace(LegacySteamGridDbKey)) { LegacySteamGridDbKey = null; return false; }
        SteamGridDbKey = LegacySteamGridDbKey;
        return LegacySteamGridDbKey is null;
    }
    public bool StartFullscreen { get; set; }
    public string LibrarySort { get; set; } = "recent";

    /// <summary>Código da língua (pt, en, es, fr, de). Vazio = a do Windows.</summary>
    public string? Language { get; set; }

    /// <summary>Cor de fundo da app (hex). Vazio = cinzento-escuro por defeito.</summary>
    public string? ThemeColor { get; set; }

    /// <summary>No Início e na página do jogo, usar a cor da capa do jogo.</summary>
    public bool UseGameColors { get; set; } = true;

    /// <summary>Fechar a janela deixa a app a correr na área de notificação (continua a contar o tempo de jogo).</summary>
    public bool CloseToTray { get; set; }

    /// <summary>Ver no GitHub, ao abrir, se há uma versão nova.</summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>Versão nova cujo aviso foi fechado (não volta a aparecer para esta versão).</summary>
    public string? DismissedUpdate { get; set; }

    /// <summary>Já se explicou que a app ficou na área de notificação (só se diz uma vez).</summary>
    public bool TrayHintShown { get; set; }

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
