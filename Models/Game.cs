using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using GameLauncher.Localization;
using System.Windows.Media;

namespace GameLauncher.Models;

/// <summary>Ficha comum de um jogo, independentemente da loja.</summary>
public sealed class Game : INotifyPropertyChanged
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Store { get; init; }
    public string? InstallPath { get; init; }
    private long _sizeBytes;
    /// <summary>Tamanho no disco. Nos jogos adicionados à mão é calculado em segundo plano (pasta do jogo).</summary>
    public long SizeBytes
    {
        get => _sizeBytes;
        set { Set(ref _sizeBytes, value); Notify(nameof(SizeText), nameof(SubtitleText)); }
    }
    public bool FullyInstalled { get; init; } = true;

    /// <summary>O que abrir para jogar: um URI do launcher (steam://, uplay://…) ou um .exe/atalho.</summary>
    public required string LaunchUri { get; init; }
    /// <summary>Argumentos (só para jogos adicionados à mão).</summary>
    public string? LaunchArguments { get; init; }
    /// <summary>Abre o jogo/launcher na loja (null = sem botão).</summary>
    public string? StoreDetailsUri { get; init; }

    /// <summary>ID numérico da Steam (só jogos Steam) — usado para as imagens oficiais.</summary>
    public string? SteamAppId { get; init; }
    /// <summary>Jogo adicionado à mão (pode ser removido).</summary>
    public bool IsCustom { get; init; }

    // ---- Dados vindos da loja ----
    public DateTime? SteamLastPlayed { get; init; }
    private int _storePlaytimeMinutes;
    /// <summary>Tempo de jogo segundo a loja (Steam: lido do PC; Epic: lido da conta, se ligada).</summary>
    public int StorePlaytimeMinutes
    {
        get => _storePlaytimeMinutes;
        set { Set(ref _storePlaytimeMinutes, value); Notify(nameof(TotalPlaytimeMinutes), nameof(PlaytimeText), nameof(PlaytimeSummary)); }
    }

    // ---- Dados que mudam enquanto a app está aberta ----
    private DateTime? _trackedLastPlayed;
    public DateTime? TrackedLastPlayed { get => _trackedLastPlayed; set { Set(ref _trackedLastPlayed, value); Notify(nameof(LastPlayed), nameof(LastPlayedText), nameof(SubtitleText)); } }

    private long _trackedSeconds;
    /// <summary>Segundos registados pela própria app (sessões).</summary>
    public long TrackedSeconds { get => _trackedSeconds; set { Set(ref _trackedSeconds, value); Notify(nameof(TotalPlaytimeMinutes), nameof(PlaytimeText), nameof(PlaytimeSummary)); } }

    private string? _coverPath, _heroPath, _logoPath, _headerPath;
    public string? CoverPath { get => _coverPath; set => Set(ref _coverPath, value); }
    public string? HeroPath { get => _heroPath; set { Set(ref _heroPath, value); Notify(nameof(BannerPath)); } }
    public string? LogoPath { get => _logoPath; set => Set(ref _logoPath, value); }
    public string? HeaderPath { get => _headerPath; set { Set(ref _headerPath, value); Notify(nameof(BannerPath)); } }

    /// <summary>Imagem larga para destaque: hero se existir, senão o cabeçalho.</summary>
    public string? BannerPath => HeroPath ?? HeaderPath;

    private Color? _accent;
    public Color? Accent { get => _accent; set => Set(ref _accent, value); }

    private bool _isFavorite;
    public bool IsFavorite { get => _isFavorite; set => Set(ref _isFavorite, value); }

    private bool _isRunning;
    public bool IsRunning { get => _isRunning; set { Set(ref _isRunning, value); Notify(nameof(PlayButtonText)); } }

    private bool _isStuck;
    /// <summary>O processo continua aberto, mas sem janela há minutos (o jogo provavelmente não fechou bem).</summary>
    public bool IsStuck { get => _isStuck; set => Set(ref _isStuck, value); }

    private bool _isLaunching;
    public bool IsLaunching { get => _isLaunching; set { Set(ref _isLaunching, value); Notify(nameof(PlayButtonText)); } }

    // ---- Valores calculados para mostrar ----
    public DateTime? LastPlayed =>
        (SteamLastPlayed, TrackedLastPlayed) switch
        {
            (null, var t) => t,
            (var s, null) => s,
            var (s, t) => s > t ? s : t,
        };

    /// <summary>Tempo total: o maior entre o da Steam e o registado pela app.</summary>
    public int TotalPlaytimeMinutes => Math.Max(StorePlaytimeMinutes, (int)(TrackedSeconds / 60));

    public string SizeText
    {
        get
        {
            if (SizeBytes <= 0) return "—";
            double gb = SizeBytes / 1024d / 1024d / 1024d;
            if (gb >= 1) return gb.ToString("0.00", Loc.Culture) + " GB";
            return (SizeBytes / 1024d / 1024d).ToString("0.0", Loc.Culture) + " MB";
        }
    }

    public string PlaytimeText => FormatMinutes(TotalPlaytimeMinutes);

    public static string FormatMinutes(int minutes)
    {
        if (minutes <= 0) return "0 min";
        if (minutes < 60) return $"{minutes} min";
        int h = minutes / 60, m = minutes % 60;
        return m == 0 ? $"{h} h" : $"{h} h {m} min";
    }

    public string LastPlayedText
    {
        get
        {
            if (LastPlayed is not { } d) return Loc.T("NeverPlayed");
            var days = (DateTime.Today - d.Date).Days;
            return days switch
            {
                0 => Loc.T("PlayedToday"),
                1 => Loc.T("PlayedYesterday"),
                < 7 => Loc.F("PlayedDaysAgo", days),
                _ => Loc.F("PlayedOn", d.ToString("d MMM yyyy", Loc.Culture)),
            };
        }
    }

    public string SubtitleText => SizeBytes > 0 ? $"{Store} · {SizeText}" : Store;

    public string PlaytimeSummary => Loc.F("PlayedFor", PlaytimeText);

    /// <summary>Com o jogo aberto, o botão de jogar passa a ser o de parar.</summary>
    public string PlayButtonText => IsRunning ? Loc.T("Game_Stop") : IsLaunching ? Loc.T("Launching") : Loc.T("Play");

    /// <summary>Volta a pedir todos os textos (depois de mudar de língua).</summary>
    public void RefreshTexts() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

    // ---- INotifyPropertyChanged ----
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void Notify(params string[] names)
    {
        foreach (var n in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
