using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using GameLauncher.Helpers;
using GameLauncher.Localization;
using GameLauncher.Models;
using GameLauncher.Scanners;

namespace GameLauncher.Services;

/// <summary>Estado central da app: lista de jogos, dados do utilizador e ações comuns.</summary>
public sealed class AppState
{
    public static AppState Current { get; } = new();

    public List<Game> Games { get; private set; } = new();
    public UserData Data { get; } = DataStore.Load();
    public List<string> Log { get; private set; } = new();
    public string? SteamRoot { get; private set; }

    /// <summary>A lista de jogos foi (re)carregada.</summary>
    public event Action? GamesLoaded;
    /// <summary>Favoritos ou coleções mudaram.</summary>
    public event Action? LibraryChanged;
    /// <summary>Foi registada uma nova sessão de jogo.</summary>
    public event Action? SessionsChanged;

    private readonly PlaytimeTracker _tracker = new();

    /// <summary>Ligação opcional à conta Epic (só para ler o tempo de jogo).</summary>
    public EpicAccount Epic { get; } = new();
    private readonly Dictionary<string, DateTime> _openSessions = new();
    private CancellationTokenSource? _artCts;

    private AppState()
    {
        _tracker.RunningChanged += (g, running) =>
            Application.Current?.Dispatcher.BeginInvoke(() => OnRunningChanged(g, running));
    }

    // ------------------------------------------------------------------ carregar

    public async Task LoadAsync()
    {
        var steam = new SteamScanner();
        var epic = new EpicScanner();
        var previous = Games.GroupBy(g => g.Id).ToDictionary(x => x.Key, x => x.First());
        var ubisoft = new UbisoftScanner { KnownSizes = previous.Values.ToDictionary(g => g.Id, g => g.SizeBytes) };
        var custom = Data.CustomGames.ToList();
        var clock = Stopwatch.StartNew();

        var games = await Task.Run(() =>
        {
            var all = new List<Game>();
            all.AddRange(Safe(steam.Scan, steam.Log));
            all.AddRange(Safe(epic.Scan, epic.Log));
            all.AddRange(WithoutDuplicates(Safe(ubisoft.Scan, ubisoft.Log), all));
            all.AddRange(CustomGames.Load(custom));
            return all;
        });
        SteamRoot = steam.SteamRoot;
        Log = steam.Log.Concat(epic.Log).Concat(ubisoft.Log).ToList();
        Log.Add(Loc.F("Log_ScanTime", games.Count, clock.ElapsedMilliseconds));
        AppLog.Info($"Biblioteca: {games.Count} jogos encontrados em {clock.ElapsedMilliseconds} ms");

        // Mantém as imagens e o tempo da Epic que já estavam no ecrã, para nada piscar
        // enquanto as imagens são confirmadas em segundo plano.
        foreach (var g in games)
        {
            if (!previous.TryGetValue(g.Id, out var old)) continue;
            g.CoverPath = old.CoverPath; g.HeroPath = old.HeroPath; g.LogoPath = old.LogoPath; g.HeaderPath = old.HeaderPath;
            if (g.StorePlaytimeMinutes == 0) g.StorePlaytimeMinutes = old.StorePlaytimeMinutes;
        }

        ApplyUserData(games);
        Games = games;
        GamesLoaded?.Invoke();

        _tracker.Start(games);
        _ = LoadArtworkAsync(games);
        _ = SyncEpicPlaytimeAsync();
    }

    /// <summary>
    /// Mostra logo a lista guardada da última vez (biblioteca.json). Devolve false se não houver.
    /// A procura a sério (<see cref="LoadAsync"/>) vem a seguir e substitui-a.
    /// </summary>
    public bool LoadSnapshot()
    {
        var clock = Stopwatch.StartNew();
        var games = LibrarySnapshot.Load();
        if (games.Count == 0) return false;
        ApplyUserData(games);
        Games = games;
        GamesLoaded?.Invoke();
        AppLog.Info($"Arranque: {games.Count} jogos mostrados da última vez em {clock.ElapsedMilliseconds} ms");
        return true;
    }

    /// <summary>Junta os dados guardados (favoritos, cores, sessões) a cada jogo.</summary>
    private void ApplyUserData(List<Game> games)
    {
        var secondsById = Data.Sessions.GroupBy(s => s.GameId)
            .ToDictionary(g => g.Key, g => (Seconds: g.Sum(s => s.Seconds), Last: g.Max(s => s.End)));

        foreach (var g in games)
        {
            g.IsFavorite = Data.Favorites.Contains(g.Id);
            if (Data.Colors.TryGetValue(g.Id, out var hex)) g.Accent = ImageHelper.FromHex(hex);
            if (secondsById.TryGetValue(g.Id, out var t))
            {
                g.TrackedSeconds = (long)t.Seconds;
                g.TrackedLastPlayed = t.Last;
            }
            if (_openSessions.ContainsKey(g.Id)) g.IsRunning = true;
        }
    }

    private static List<Game> Safe(Func<List<Game>> scan, List<string> log)
    {
        try { return scan(); }
        catch (Exception ex) { log.Add(ex.Message); return new List<Game>(); }
    }

    /// <summary>
    /// Jogos da Ubisoft comprados na Steam ou na Epic também aparecem no Ubisoft Connect.
    /// Fica só a versão da outra loja (é por ela que o jogo deve ser aberto).
    /// </summary>
    private static IEnumerable<Game> WithoutDuplicates(List<Game> candidates, List<Game> existing)
    {
        static string Norm(string? path) => (path ?? "").Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();
        static string Simple(string name) =>
            new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        var paths = existing.Select(g => Norm(g.InstallPath)).Where(p => p.Length > 0).ToHashSet();
        var names = existing.Select(g => Simple(g.Name)).ToHashSet();
        foreach (var g in candidates)
        {
            var p = Norm(g.InstallPath);
            if (paths.Contains(p) || p.Contains(@"\steamapps\common\") || names.Contains(Simple(g.Name))) continue;
            yield return g;
        }
    }

    // ------------------------------------------------------------ lojas e jogos manuais

    /// <summary>Lojas com jogos, por ordem: Steam, Epic, Ubisoft e depois as criadas à mão (alfabética).</summary>
    public List<(string Name, int Count)> Stores
    {
        get
        {
            var order = new[] { "Steam", EpicScanner.StoreName, UbisoftScanner.StoreName };
            return Games.GroupBy(g => g.Store)
                .Select(g => (Name: g.Key, Count: g.Count()))
                .OrderBy(s => Array.IndexOf(order, s.Name) is var i && i >= 0 ? i : 99)
                .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }

    public async Task<Game?> AddCustomGameAsync(CustomGame c, IReadOnlyDictionary<ArtKind, string>? art = null)
    {
        Data.CustomGames.Add(c);
        if (art is { Count: > 0 })
        {
            var custom = new CustomArtwork();
            foreach (var (kind, file) in art)
            {
                try { CustomArtStore.Set(custom, kind, CustomArtStore.Import(file, c.Id, kind)); } catch { }
            }
            if (!custom.IsEmpty) Data.CustomArt[c.Id] = custom;
        }
        Save();
        await LoadAsync();
        return FindGame(c.Id);
    }

    public async Task RemoveCustomGameAsync(Game game)
    {
        Data.CustomGames.RemoveAll(c => c.Id == game.Id);
        Data.Favorites.Remove(game.Id);
        if (Data.CustomArt.Remove(game.Id, out var art))
            foreach (var kind in Enum.GetValues<ArtKind>()) CustomArtStore.Delete(CustomArtStore.Get(art, kind));
        foreach (var col in Data.Collections) col.GameIds.Remove(game.Id);
        Save();
        await LoadAsync();
    }

    private async Task LoadArtworkAsync(List<Game> games, IProgress<(int Done, int Total)>? progress = null)
    {
        _artCts?.Cancel();
        var cts = _artCts = new CancellationTokenSource();
        var art = new ArtworkService(SteamRoot, Data.Settings.SteamGridDbKey);

        // Primeiro os jogos mais recentes, para o Início ficar pronto depressa.
        var ordered = games.OrderByDescending(g => g.LastPlayed ?? DateTime.MinValue).ToList();
        int done = 0;
        progress?.Report((0, ordered.Count));

        await Parallel.ForEachAsync(ordered, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cts.Token },
            async (g, ct) =>
            {
                try { await LoadArtworkForAsync(art, g, ct); }
                catch (OperationCanceledException) { throw; }
                catch { /* uma imagem falhada não deve parar as outras */ }
                progress?.Report((Interlocked.Increment(ref done), ordered.Count));
            }).ContinueWith(_ => { });

        if (!cts.IsCancellationRequested)
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Save();
                var snapshot = Games.ToList();
                _ = Task.Run(() => LibrarySnapshot.Save(snapshot));
            });
    }

    private async Task LoadArtworkForAsync(ArtworkService art, Game g, CancellationToken ct)
    {
        // As imagens escolhidas à mão têm prioridade; só se procura o que faltar.
        Data.CustomArt.TryGetValue(g.Id, out var custom);
        var cover = CustomArtStore.Get(custom, ArtKind.Cover) ?? await art.GetAsync(g, ArtKind.Cover, ct);
        var hero = CustomArtStore.Get(custom, ArtKind.Hero) ?? await art.GetAsync(g, ArtKind.Hero, ct);
        var logo = CustomArtStore.Get(custom, ArtKind.Logo) ?? await art.GetAsync(g, ArtKind.Logo, ct);
        var header = CustomArtStore.Get(custom, ArtKind.Header)
                     ?? await art.GetAsync(g, ArtKind.Header, ct);

        Color? accent = g.Accent ?? ImageHelper.DominantColor(cover ?? header ?? hero);

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            g.CoverPath = cover;
            g.HeroPath = hero;
            g.LogoPath = logo;
            g.HeaderPath = header;
            if (g.Accent is null && accent is { } c)
            {
                g.Accent = c;
                Data.Colors[g.Id] = ImageHelper.ToHex(c);
            }
        });
    }

    /// <summary>Volta a procurar imagens (ex.: depois de pôr a chave do SteamGridDB). Termina quando a procura acabar.</summary>
    public Task ReloadArtworkAsync(bool retryMissing, IProgress<(int Done, int Total)>? progress = null)
    {
        if (retryMissing)
        {
            try { foreach (var f in Directory.EnumerateFiles(DataStore.CacheDir, "*.none")) File.Delete(f); } catch { }
        }
        return LoadArtworkAsync(Games, progress);
    }

    // ------------------------------------------------------------ imagens escolhidas à mão

    public bool HasCustomArt(Game g, ArtKind kind) =>
        Data.CustomArt.TryGetValue(g.Id, out var art) && CustomArtStore.Get(art, kind) is not null;

    /// <summary>Usa uma imagem escolhida pelo utilizador (null = volta à automática) e atualiza o jogo.</summary>
    public async Task SetCustomArtAsync(Game g, ArtKind kind, string? sourceFile)
    {
        if (!Data.CustomArt.TryGetValue(g.Id, out var art)) Data.CustomArt[g.Id] = art = new CustomArtwork();
        var old = CustomArtStore.Get(art, kind);
        CustomArtStore.Set(art, kind, sourceFile is null ? null : CustomArtStore.Import(sourceFile, g.Id, kind));
        CustomArtStore.Delete(old);
        if (art.IsEmpty) Data.CustomArt.Remove(g.Id);

        // A cor do jogo vem da capa: com capa nova, recalcula-a.
        if (kind == ArtKind.Cover || g.CoverPath is null)
        {
            g.Accent = null;
            Data.Colors.Remove(g.Id);
        }
        Save();

        try { await LoadArtworkForAsync(new ArtworkService(SteamRoot, Data.Settings.SteamGridDbKey), g, CancellationToken.None); }
        catch (Exception ex) { AppLog.Error("Imagem personalizada", ex); }
        Save();
        var snapshot = Games.ToList();
        _ = Task.Run(() => LibrarySnapshot.Save(snapshot));
        LibraryChanged?.Invoke();
    }

    /// <summary>Se a conta Epic estiver ligada, vai buscar o tempo de jogo e junta-o aos jogos da Epic.</summary>
    public async Task<bool> SyncEpicPlaytimeAsync()
    {
        if (!Epic.IsConnected) return false;
        var minutes = await Epic.GetPlaytimeAsync();
        if (minutes is null)
        {
            Log.Add(Loc.T("Log_EpicPlaytimeFailed"));
            return false;
        }
        int matched = 0;
        foreach (var g in Games.Where(g => g.Id.StartsWith("epic:")))
            if (minutes.TryGetValue(g.Id["epic:".Length..], out var m)) { g.StorePlaytimeMinutes = m; matched++; }
        Log.Add(Loc.F("Log_EpicPlaytimeRead", matched));
        SessionsChanged?.Invoke(); // a página de tempo de jogo atualiza-se
        return true;
    }

    // ------------------------------------------------------------------ consultas

    public IEnumerable<Game> RecentGames =>
        Games.OrderByDescending(g => g.LastPlayed ?? DateTime.MinValue)
             .ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase);

    public Game? Featured => RecentGames.FirstOrDefault();

    public Game? FindGame(string id) => Games.FirstOrDefault(g => g.Id == id);

    public IEnumerable<Game> GamesIn(GameCollection c) =>
        c.GameIds.Select(FindGame).Where(g => g is not null)!;

    // ------------------------------------------------------------------ ações

    public void Play(Game game)
    {
        if (game.IsRunning) return;
        try
        {
            var psi = new ProcessStartInfo(game.LaunchUri) { UseShellExecute = true };
            if (game.LaunchArguments is { } args) psi.Arguments = args;
            if (game.IsCustom && game.InstallPath is { } dir) psi.WorkingDirectory = dir;
            Process.Start(psi);
            game.IsLaunching = true;
            // Se ao fim de 45 s o jogo não aparecer, volta a mostrar "Jogar".
            _ = Task.Delay(TimeSpan.FromSeconds(45)).ContinueWith(_ =>
                Application.Current?.Dispatcher.BeginInvoke(() => game.IsLaunching = false));
        }
        catch (Exception ex)
        {
            MessageBox.Show(Loc.F("Err_CannotOpen", game.Name) + "\n\n" + ex.Message, Loc.T("Err_Title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void OpenFolder(Game game)
    {
        if (game.InstallPath is { } path && Directory.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        else
            MessageBox.Show(Loc.T("Err_FolderNotFound"), game.Name,
                MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void OpenInStore(Game game)
    {
        if (game.StoreDetailsUri is null) return;
        try { Process.Start(new ProcessStartInfo(game.StoreDetailsUri) { UseShellExecute = true }); } catch { }
    }

    public void ToggleFavorite(Game game)
    {
        game.IsFavorite = !game.IsFavorite;
        if (game.IsFavorite) Data.Favorites.Add(game.Id); else Data.Favorites.Remove(game.Id);
        Save();
        LibraryChanged?.Invoke();
    }

    public GameCollection? CreateCollection(string name)
    {
        name = name.Trim();
        if (name.Length == 0) return null;
        var existing = Data.Collections.FirstOrDefault(c => c.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
        if (existing is not null) return existing;

        var c = new GameCollection { Name = name };
        Data.Collections.Add(c);
        Save();
        LibraryChanged?.Invoke();
        return c;
    }

    public void DeleteCollection(GameCollection c)
    {
        Data.Collections.Remove(c);
        Save();
        LibraryChanged?.Invoke();
    }

    public void SetInCollection(GameCollection c, Game g, bool inside)
    {
        if (inside && !c.GameIds.Contains(g.Id)) c.GameIds.Add(g.Id);
        if (!inside) c.GameIds.Remove(g.Id);
        Save();
        LibraryChanged?.Invoke();
    }

    public void Save() => DataStore.Save(Data);

    // ------------------------------------------------------------------ tempo de jogo

    private void OnRunningChanged(Game tracked, bool running)
    {
        var game = FindGame(tracked.Id) ?? tracked;

        if (running)
        {
            _openSessions.TryAdd(game.Id, DateTime.Now);
            game.IsRunning = true;
            game.IsLaunching = false;
        }
        else
        {
            game.IsRunning = false;
            CloseSession(game);
        }
    }

    private void CloseSession(Game game)
    {
        if (!_openSessions.Remove(game.Id, out var start)) return;
        var end = DateTime.Now;
        if ((end - start).TotalSeconds < 60) return; // ignora aberturas de poucos segundos

        Data.Sessions.Add(new PlaySession { GameId = game.Id, Start = start, End = end });
        game.TrackedSeconds += (long)(end - start).TotalSeconds;
        game.TrackedLastPlayed = end;
        Save();
        SessionsChanged?.Invoke();
    }

    /// <summary>Fecha sessões abertas quando a app é fechada.</summary>
    public void Shutdown()
    {
        _tracker.Stop();
        foreach (var id in _openSessions.Keys.ToList())
            if (FindGame(id) is { } g) CloseSession(g);
        Save();
    }
}
