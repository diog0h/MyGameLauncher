using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using GameLauncher.Helpers;
using GameLauncher.Localization;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class SettingsPage : UserControl, IPage
{
    public string Title => Loc.T("Nav_Settings");

    private AppState State => AppState.Current;
    private bool _ready;
    private readonly DispatcherTimer _controllersTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    /// <summary>(chave do texto da ação, teclado, Xbox, PlayStation) — as três últimas em ícones (ver ButtonIcons).</summary>
    private static readonly (string Action, string Keyboard, string Xbox, string PlayStation)[] Shortcuts =
    {
        ("Sh_Move", "keys:arrows", "xb:dpad / stick:L", "ps:dpad / stick:L"),
        ("Sh_Open", "key:Enter", "xb:A", "ps:cross"),
        ("Sh_Back", "key:Esc / key:Backspace", "xb:B", "ps:circle"),
        ("Sh_PlaySelected", "—", "xb:X", "ps:square"),
        ("Sh_Favorite", "—", "xb:Y", "ps:triangle"),
        ("Sh_Sections", "—", "xb:LB / xb:RB", "ps:L1 / ps:R1"),
        ("Sh_Home", "—", "xb:view", "ps:home / ps:create"),
        ("Fullscreen", "key:F11", "xb:menu", "ps:options"),
        ("Sh_Search", "key:Ctrl + key:F", "—", "—"),
        ("Sh_Reload", "key:F5", "—", "—"),
    };

    public SettingsPage()
    {
        InitializeComponent();

        KeyBox.Text = State.Data.Settings.SteamGridDbKey ?? "";
        KeyStatus.Text = Loc.T(string.IsNullOrWhiteSpace(KeyBox.Text) ? "Set_NoKey" : "Set_KeySet");
        FullscreenCheck.IsChecked = State.Data.Settings.StartFullscreen;
        BuildLanguageChips();
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = Loc.F("About_Version", v is null ? "?" : $"{v.Major}.{v.Minor}.{v.Build}");
        AuthorText.Text = Loc.F("About_MadeBy", "diog0_h");
        BuildShortcuts();
        _ready = true;

        _controllersTimer.Tick += (_, _) => RefreshControllers();
        Loaded += (_, _) =>
        {
            RefreshInfo(); RefreshControllers(); RefreshEpic(); _controllersTimer.Start();
            var w = Window.GetWindow(this);
            if (w is not null) w.Activated += Window_Activated;
        };
        Unloaded += (_, _) =>
        {
            _controllersTimer.Stop();
            var w = Window.GetWindow(this);
            if (w is not null) w.Activated -= Window_Activated;
        };
    }

    // ------------------------------------------------------------ idioma

    private void BuildLanguageChips()
    {
        foreach (var lang in Loc.Languages)
        {
            var chip = new RadioButton
            {
                Content = lang.Name,
                GroupName = "Language",
                Tag = lang.Code,
                Style = (Style)FindResource("ChipRadio"),
                Margin = new Thickness(0, 0, 8, 8),
                IsChecked = lang.Code == Loc.Code,
            };
            chip.Checked += Language_Checked;
            LanguageChips.Children.Add(chip);
        }
    }

    private void Language_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready || (sender as FrameworkElement)?.Tag is not string code || code == Loc.Code) return;
        State.Data.Settings.Language = code;
        State.Save();
        // Adia um pouco para o clique terminar antes de a página ser recriada na nova língua.
        Dispatcher.BeginInvoke(() => Loc.SetLanguage(code), DispatcherPriority.Background);
    }

    // ------------------------------------------------------------ conta Epic (só tempo de jogo)

    private void RefreshEpic()
    {
        var s = State.Epic.Current;
        EpicDisconnected.Visibility = s is null ? Visibility.Visible : Visibility.Collapsed;
        EpicConnected.Visibility = s is null ? Visibility.Collapsed : Visibility.Visible;
        if (s is null) return;
        EpicConnectedText.Text = Loc.F("Set_EpicConnectedAs", string.IsNullOrEmpty(s.DisplayName) ? "Epic Games" : s.DisplayName);
        EpicSyncText.Text = s.LastSync is { } t
            ? Loc.F("Set_EpicLastSync", t.ToString("g", Loc.Culture))
            : Loc.T("Set_EpicNeverSynced");
    }

    private void ShowEpicStatus(string text)
    {
        EpicStatus.Text = text;
        EpicStatus.Visibility = Visibility.Visible;
    }

    private void EpicOpenLogin_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(EpicAccount.LoginUrl) { UseShellExecute = true }); }
        catch { }
    }

    /// <summary>Ao voltar à app depois de copiar o texto da página da Epic, cola o código sozinho.</summary>
    private void Window_Activated(object? sender, EventArgs e)
    {
        if (State.Epic.IsConnected || EpicCodeBox.Text.Length > 0) return;
        try
        {
            if (Clipboard.ContainsText() && EpicAccount.ExtractCode(Clipboard.GetText()) is { } code)
            {
                EpicCodeBox.Text = code;
                ShowEpicStatus(Loc.T("Set_EpicCodeDetected"));
            }
        }
        catch { /* área de transferência ocupada por outra app */ }
    }

    private async void EpicConnect_Click(object sender, RoutedEventArgs e)
    {
        var code = EpicAccount.ExtractCode(EpicCodeBox.Text);
        if (code is null) { ShowEpicStatus(Loc.T("Set_EpicBadCode")); return; }

        EpicConnectButton.IsEnabled = false;
        ShowEpicStatus(Loc.T("Set_EpicConnecting"));
        var (ok, error) = await State.Epic.ConnectAsync(code);
        EpicConnectButton.IsEnabled = true;

        if (!ok) { ShowEpicStatus(Loc.F("Set_EpicError", error ?? "?")); return; }
        EpicCodeBox.Text = "";
        try { Clipboard.Clear(); } catch { } // o código já não é preciso
        await SyncEpic();
    }

    private async void EpicSync_Click(object sender, RoutedEventArgs e) => await SyncEpic();

    private async Task SyncEpic()
    {
        ShowEpicStatus(Loc.T("Set_EpicSyncing"));
        var ok = await State.SyncEpicPlaytimeAsync();
        ShowEpicStatus(Loc.T(ok ? "Set_EpicSynced" : "Set_EpicSyncFailed"));
        RefreshEpic();
        RefreshInfo();
    }

    private async void EpicDisconnect_Click(object sender, RoutedEventArgs e)
    {
        await State.Epic.DisconnectAsync();
        ShowEpicStatus(Loc.T("Set_EpicDisconnected"));
        RefreshEpic();
    }

    // ------------------------------------------------------------ informação

    private void RefreshInfo()
    {
        LogBox.Text = string.Join(Environment.NewLine, State.Log)
                      + Environment.NewLine + Loc.F("Set_AppData", DataStore.Root);

        int withCover = State.Games.Count(g => g.CoverPath is not null);
        long bytes = 0;
        try { bytes = new DirectoryInfo(DataStore.CacheDir).EnumerateFiles().Sum(f => f.Length); } catch { }
        CacheText.Text = Loc.F("Set_CacheInfo", withCover, State.Games.Count, bytes / 1024d / 1024d);
    }

    private void RefreshControllers()
    {
        var pad = MainWindow.Current.Gamepad;
        var names = new List<string>();
        for (int i = 0; i < pad.XboxControllers; i++) names.Add("Xbox");
        names.AddRange(pad.PlayStationControllers);
        ControllersText.Text = Loc.F("Set_Controllers", names.Count == 0 ? Loc.T("Set_NoControllers") : string.Join(", ", names));
    }

    private void BuildShortcuts()
    {
        void Put(UIElement e, int row, int col)
        {
            Grid.SetRow(e, row);
            Grid.SetColumn(e, col);
            ShortcutsGrid.Children.Add(e);
        }

        TextBlock Text(string text, bool header) => new()
        {
            Text = text,
            Margin = new Thickness(0, 6, 12, 6),
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = (Brush)FindResource(header ? "SubtleTextBrush" : "TextBrush"),
        };

        ShortcutsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Put(Text(Loc.T("Sh_Action"), true), 0, 0);
        Put(Text(Loc.T("Sh_Keyboard"), true), 0, 1);
        Put(Text("Xbox", true), 0, 2);
        Put(Text("PlayStation", true), 0, 3);

        for (int i = 0; i < Shortcuts.Length; i++)
        {
            var s = Shortcuts[i];
            int row = i + 1;
            ShortcutsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Put(Text(Loc.T(s.Action), false), row, 0);
            var cells = new[] { s.Keyboard, s.Xbox, s.PlayStation };
            for (int c = 0; c < cells.Length; c++)
            {
                var icons = ButtonIcons.Row(cells[c]);
                icons.Margin = new Thickness(0, 5, 12, 5);
                icons.HorizontalAlignment = HorizontalAlignment.Left;
                Put(icons, row, c + 1);
            }
        }
    }

    // ------------------------------------------------------------ sobre

    private void Link_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch { }
        e.Handled = true;
    }

    private void Licenses_Click(object sender, RoutedEventArgs e)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.txt");
        try
        {
            if (File.Exists(file))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file) { UseShellExecute = true });
            else
                MessageBox.Show(Loc.T("About_NoLicenseFile"), "MyGameLauncher", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch { }
    }

    // ------------------------------------------------------------ ações

    private void SaveKey_Click(object sender, RoutedEventArgs e)
    {
        var key = KeyBox.Text.Trim();
        State.Data.Settings.SteamGridDbKey = key.Length == 0 ? null : key;
        State.Save();
        KeyStatus.Text = Loc.T(key.Length == 0 ? "Set_KeyRemoved" : "Set_KeySaved");
        State.ReloadArtwork(retryMissing: true);
    }

    private void RetryArt_Click(object sender, RoutedEventArgs e)
    {
        State.ReloadArtwork(retryMissing: true);
        CacheText.Text = Loc.T("Set_Searching");
    }

    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        ArtworkService.ClearCache();
        State.Data.Colors.Clear();
        foreach (var g in State.Games) g.Accent = null;
        State.ReloadArtwork(retryMissing: true);
        CacheText.Text = Loc.T("Set_CacheCleared");
    }

    private void Fullscreen_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        State.Data.Settings.StartFullscreen = FullscreenCheck.IsChecked == true;
        State.Save();
        MainWindow.Current.SetFullscreen(State.Data.Settings.StartFullscreen);
    }

    private async void Reload_Click(object sender, RoutedEventArgs e)
    {
        await MainWindow.Current.ReloadAsync();
        RefreshInfo();
    }
}
