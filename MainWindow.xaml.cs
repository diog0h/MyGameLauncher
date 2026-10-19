using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GameLauncher.Helpers;
using GameLauncher.Localization;
using GameLauncher.Models;
using GameLauncher.Services;
using GameLauncher.Views;

namespace GameLauncher;

public partial class MainWindow : Window
{
    public static MainWindow Current { get; private set; } = null!;

    /// <summary>Cores que se podem escolher nas Definições (a primeira é a por defeito).</summary>
    public static readonly (string Key, string Hex)[] ThemeColors =
    {
        ("Color_Grey", "#2A2B33"),
        ("Color_Red", "#B3202A"),
        ("Color_Blue", "#1F4E8C"),
        ("Color_Purple", "#4B2C7A"),
        ("Color_Green", "#1E5C3A"),
    };

    /// <summary>Cor de fundo escolhida pelo utilizador (cinzento-escuro se não escolheu).</summary>
    public static Color DefaultAccent =>
        ImageHelper.FromHex(AppState.Current.Data.Settings.ThemeColor) ?? ImageHelper.FromHex(ThemeColors[0].Hex)!.Value;

    private readonly SolidColorBrush _themeBrush = new(DefaultAccent);
    private readonly Stack<(UserControl Page, RadioButton? Nav)> _history = new();
    private (UserControl Page, RadioButton? Nav)? _current;
    private readonly GamepadService _gamepad = new();
    public GamepadService Gamepad => _gamepad;

    // A Steam (configuração de ambiente de trabalho) pode transformar o comando em teclas.
    // Se uma tecla igual chegar logo a seguir a um botão do comando, é ignorada para não andar duas vezes.
    private PadButton? _lastPad;
    private DateTime _lastPadTime;
    private Key? _lastKey;
    private DateTime _lastKeyTime;
    private bool _isFullscreen;
    private WindowState _stateBeforeFullscreen;

    private AppState State => AppState.Current;

    public MainWindow()
    {
        Current = this;
        InitializeComponent();
        Root.Background = _themeBrush;
        RestoreWindowPlacement();
        TopBarBackground.Background = _themeBrush; // mesma cor (e mesma animação) do fundo
        ContentHost.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnContentScrolled));

        SourceInitialized += (_, _) =>
        {
            NativeMethods.UseDarkTitleBar(this);
            NativeMethods.SetCaptionColor(this, DefaultAccent);
        };
        Loaded += async (_, _) => await StartAsync();
        Closing += (_, _) => { SaveWindowPlacement(); _gamepad.Dispose(); State.Shutdown(); };

        PreviewKeyDown += OnPreviewKeyDown;
        PreviewMouseMove += (_, e) =>
        {
            // O WPF também envia "MouseMove" quando o conteúdo mexe: só conta movimento real do rato.
            var pos = e.GetPosition(this);
            if (_lastMouse is { } last && (Math.Abs(pos.X - last.X) > 4 || Math.Abs(pos.Y - last.Y) > 4)) HideHints();
            _lastMouse = pos;
        };
        PreviewMouseDown += (_, _) => HideHints();
        PreviewGotKeyboardFocus += (_, _) =>
        {
            if (_hintsVisible) Dispatcher.BeginInvoke(UpdateHints, System.Windows.Threading.DispatcherPriority.Background);
        };
        _gamepad.Pressed += OnGamepad;
        _gamepad.Start();

        State.LibraryChanged += RefreshSidebar;
        State.GamesLoaded += RefreshSidebar;
        Loc.Changed += OnLanguageChanged;
    }

    private async Task StartAsync()
    {
        if (State.Data.Settings.StartFullscreen) SetFullscreen(true);
        await State.LoadAsync();
        Navigate(new HomePage(), NavHome, addToHistory: false);
    }

    // ============================================================ navegação

    public void Navigate(UserControl page, RadioButton? nav = null, bool addToHistory = true)
    {
        if (_current is { } cur && addToHistory) _history.Push(cur);
        Show(page, nav);
    }

    private void Show(UserControl page, RadioButton? nav)
    {
        _current = (page, nav);
        ContentHost.Content = page;

        // Marca o item certo na barra lateral (ou nenhum).
        foreach (var rb in AllNavItems()) rb.IsChecked = rb == nav;

        PageTitle.Text = page is IPage p ? p.Title : "";
        // Só o Início e a página do jogo têm cor própria (definem-na ao abrir); as outras voltam à cor original.
        if (page is not HomePage and not GamePage) SetAccent(null);
        SetTopBarSolid(false);
        // Ao voltar atrás a página pode estar a meio: verifica depois de ser desenhada.
        Dispatcher.BeginInvoke(() =>
        {
            if (FindDescendant<ScrollViewer>(page) is { } sv) SetTopBarSolid(sv.VerticalOffset > 2);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
        BackButton.Visibility = _history.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Pequena animação de entrada.
        page.Opacity = 0;
        page.RenderTransform = new TranslateTransform(0, 12);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        page.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
        page.RenderTransform.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
    }

    public void GoBack()
    {
        if (_history.Count == 0) return;
        var (page, nav) = _history.Pop();
        Show(page, nav);
        (page as IPage)?.OnReturned();
    }

    public void GoHome()
    {
        _history.Clear();
        SearchBox.Text = "";
        Show(new HomePage(), NavHome);
    }

    public void OpenLibrary(LibraryMode mode, GameCollection? collection = null, string? store = null)
    {
        var nav = mode switch
        {
            LibraryMode.Favorites => NavFavorites,
            LibraryMode.Collection => FindCollectionNav(collection),
            LibraryMode.Store => FindStoreNav(store),
            _ => NavAll,
        };
        Navigate(new LibraryPage(mode, collection, store), nav);
    }

    public sealed record StoreItem(string Name, int Count);

    private IEnumerable<RadioButton> StoreNavItems() => NavItemsIn(StoresList);

    private RadioButton? FindStoreNav(string? store) =>
        StoreNavItems().FirstOrDefault(rb => rb.Tag as string == store);

    private void NavStore_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string store)
            Navigate(new LibraryPage(LibraryMode.Store, null, store), sender as RadioButton);
    }

    private void AddGame_Click(object sender, RoutedEventArgs e) => Navigate(new AddGamePage(), null);

    private IEnumerable<RadioButton> AllNavItems()
    {
        yield return NavHome;
        yield return NavAll;
        yield return NavFavorites;
        yield return NavPlaytime;
        yield return NavSettings;
        foreach (var rb in StoreNavItems()) yield return rb;
        foreach (var rb in CollectionNavItems()) yield return rb;
    }

    private IEnumerable<RadioButton> CollectionNavItems() => NavItemsIn(CollectionsList);

    private static IEnumerable<RadioButton> NavItemsIn(ItemsControl list)
    {
        for (int i = 0; i < list.Items.Count; i++)
            if (list.ItemContainerGenerator.ContainerFromIndex(i) is ContentPresenter cp
                && VisualTreeHelper.GetChildrenCount(cp) > 0
                && VisualTreeHelper.GetChild(cp, 0) is RadioButton rb)
                yield return rb;
    }

    private RadioButton? FindCollectionNav(GameCollection? c) =>
        CollectionNavItems().FirstOrDefault(rb => rb.Tag == c);

    private void NavHome_Click(object sender, RoutedEventArgs e) => GoHome();
    private void NavAll_Click(object sender, RoutedEventArgs e) => OpenLibrary(LibraryMode.All);
    private void NavFavorites_Click(object sender, RoutedEventArgs e) => OpenLibrary(LibraryMode.Favorites);
    private void NavPlaytime_Click(object sender, RoutedEventArgs e) => Navigate(new PlaytimePage(), NavPlaytime);
    private void NavSettings_Click(object sender, RoutedEventArgs e) => Navigate(new SettingsPage(), NavSettings);
    private void Back_Click(object sender, RoutedEventArgs e) => GoBack();

    private void NavCollection_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is GameCollection c)
            Navigate(new LibraryPage(LibraryMode.Collection, c), sender as RadioButton);
    }

    // ============================================================ barra lateral

    private void RefreshSidebar()
    {
        AllCount.Text = State.Games.Count.ToString();
        FavCount.Text = State.Games.Count(g => g.IsFavorite).ToString();
        CollectionsList.ItemsSource = null;
        CollectionsList.ItemsSource = State.Data.Collections.ToList();
        StoresList.ItemsSource = State.Stores.Select(s => new StoreItem(s.Name, s.Count)).ToList();

        // Volta a marcar a coleção aberta (os botões foram recriados).
        if (_current is { Page: LibraryPage lib } cur && (lib.Collection is not null || lib.StoreName is not null))
        {
            CollectionsList.UpdateLayout();
            StoresList.UpdateLayout();
            var nav = lib.Collection is { } c ? FindCollectionNav(c) : FindStoreNav(lib.StoreName);
            if (nav is not null) { nav.IsChecked = true; _current = (cur.Page, nav); }
        }
    }

    private void NewCollection_Click(object sender, RoutedEventArgs e)
    {
        NewCollectionBox.Text = "";
        NewCollectionBox.Visibility = Visibility.Visible;
        NewCollectionBox.Focus();
    }

    private void NewCollectionBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var c = State.CreateCollection(NewCollectionBox.Text);
            NewCollectionBox.Visibility = Visibility.Collapsed;
            if (c is not null)
            {
                CollectionsList.UpdateLayout();
                Navigate(new LibraryPage(LibraryMode.Collection, c), FindCollectionNav(c));
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            NewCollectionBox.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
    }

    private void NewCollectionBox_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NewCollectionBox.Text)) NewCollectionBox.Visibility = Visibility.Collapsed;
    }

    // ============================================================ pesquisa

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var q = SearchBox.Text.Trim();
        if (_current?.Page is LibraryPage lib && lib.Mode == LibraryMode.All)
        {
            lib.Query = q;
            return;
        }
        if (q.Length == 0) return;
        Navigate(new LibraryPage(LibraryMode.All) { Query = q }, NavAll);
    }

    // ============================================================ língua

    private void OnLanguageChanged()
    {
        // Textos ligados por {l:Tr} já mudaram sozinhos; falta o que é criado no código.
        foreach (var g in State.Games) g.RefreshTexts();
        FullscreenText.Text = Loc.T(_isFullscreen ? "ExitFullscreen" : "Fullscreen");

        // Recria a página atual na nova língua; o histórico tinha páginas na língua antiga.
        _history.Clear();
        if (_current is { } cur)
        {
            UserControl fresh = cur.Page switch
            {
                SettingsPage => new SettingsPage(),
                PlaytimePage => new PlaytimePage(),
                LibraryPage lib => new LibraryPage(lib.Mode, lib.Collection, lib.StoreName) { Query = lib.Query },
                AddGamePage => new AddGamePage(),
                GamePage gp when gp.ContextGame is { } g => new GamePage(g),
                _ => new HomePage(),
            };
            Show(fresh, cur.Nav);
        }
    }

    // ============================================================ tamanho e posição da janela

    /// <summary>Abre como da última vez; se não houver (ou não couber no ecrã), abre centrada num tamanho que cabe.</summary>
    private void RestoreWindowPlacement()
    {
        var work = SystemParameters.WorkArea; // ecrã principal sem a barra de tarefas
        var saved = State.Data.Settings.Window;

        if (saved is not null && FitsOnScreen(saved))
        {
            Left = saved.Left; Top = saved.Top; Width = saved.Width; Height = saved.Height;
            if (saved.Maximized) WindowState = WindowState.Maximized;
            return;
        }

        Width = Math.Max(MinWidth, Math.Min(1360, work.Width - 80));
        Height = Math.Max(MinHeight, Math.Min(860, work.Height - 60));
        Left = work.Left + (work.Width - Width) / 2;
        Top = work.Top + Math.Max(0, (work.Height - Height) / 2);
        if (saved?.Maximized == true) WindowState = WindowState.Maximized;
    }

    /// <summary>A janela guardada tem de caber num ecrã e ter a barra de título visível.</summary>
    private static bool FitsOnScreen(WindowPlacement p)
    {
        if (p.Width < 400 || p.Height < 300) return false;
        double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
        double vr = vl + SystemParameters.VirtualScreenWidth, vb = vt + SystemParameters.VirtualScreenHeight;
        bool titleVisible = p.Top >= vt && p.Top + 30 <= vb && p.Left + 100 <= vr && p.Left + p.Width - 100 >= vl;
        bool notTooBig = p.Width <= SystemParameters.VirtualScreenWidth && p.Height <= SystemParameters.VirtualScreenHeight;
        return titleVisible && notTooBig;
    }

    private void SaveWindowPlacement()
    {
        // Em ecrã inteiro guarda o tamanho "normal" e se estava maximizada antes.
        var bounds = WindowState == WindowState.Normal && !_isFullscreen ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (bounds.IsEmpty || bounds.Width <= 0) return;
        State.Data.Settings.Window = new WindowPlacement
        {
            Left = bounds.Left, Top = bounds.Top, Width = bounds.Width, Height = bounds.Height,
            Maximized = _isFullscreen ? _stateBeforeFullscreen == WindowState.Maximized : WindowState == WindowState.Maximized,
        };
        // State.Shutdown() grava a seguir.
    }

    // ============================================================ barra de ajuda do comando

    private bool _hintsVisible;
    private Point? _lastMouse;

    private void ShowHints()
    {
        if (_hintsVisible) return;
        _hintsVisible = true;
        HintBar.Visibility = Visibility.Visible;
        HintBar.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)));
        ContentHost.Margin = new Thickness(0, 0, 0, HintBar.Height); // o conteúdo não fica tapado
    }

    private void HideHints()
    {
        if (!_hintsVisible) return;
        _hintsVisible = false;
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180));
        fade.Completed += (_, _) => { if (!_hintsVisible) HintBar.Visibility = Visibility.Collapsed; };
        HintBar.BeginAnimation(OpacityProperty, fade);
        ContentHost.Margin = new Thickness(0);
    }

    /// <summary>Mostra só as ações que fazem sentido agora, com os ícones do comando em uso.</summary>
    private void UpdateHints()
    {
        bool ps = _gamepad.LastKind == PadKind.PlayStation;
        string Icon(string xbox, string playstation) => ps ? playstation : xbox;

        HintsLeft.Children.Clear();
        HintsRight.Children.Clear();

        AddHint(HintsLeft, Icon("xb:view", "ps:home"), "Hint_Home");
        AddHint(HintsLeft, Icon("xb:LB + xb:RB", "ps:L1 + ps:R1"), "Hint_Sections");

        AddHint(HintsRight, Icon("xb:menu", "ps:options"), _isFullscreen ? "Hint_ExitFullscreen" : "Hint_Fullscreen");
        if (TargetGame() is not null)
        {
            AddHint(HintsRight, Icon("xb:Y", "ps:triangle"), "Hint_Favorite");
            AddHint(HintsRight, Icon("xb:X", "ps:square"), "Hint_Play");
        }
        if (Keyboard.FocusedElement is ButtonBase)
            AddHint(HintsRight, Icon("xb:A", "ps:cross"), "Hint_Select");
        if (_history.Count > 0 || _isFullscreen)
            AddHint(HintsRight, Icon("xb:B", "ps:circle"), "Hint_Back");
    }

    private static void AddHint(Panel panel, string icons, string labelKey)
    {
        var item = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = panel.Children.Count == 0 ? new Thickness(0) : new Thickness(30, 0, 0, 0),
        };
        var row = ButtonIcons.Row(icons.Replace("+", ""));
        item.Children.Add(row);
        item.Children.Add(new TextBlock
        {
            Text = Loc.T(labelKey).ToUpper(Loc.Culture),
            FontSize = 13.5, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0),
        });
        panel.Children.Add(item);
    }

    // ============================================================ barra de cima

    private bool _topBarSolid;

    private void OnContentScrolled(object sender, ScrollChangedEventArgs e)
    {
        // Só interessa o scroll vertical da página (não as filas horizontais nem caixas de texto).
        if (e.OriginalSource is not ScrollViewer sv) return;
        if (sv.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled || sv.TemplatedParent is TextBox) return;
        SetTopBarSolid(sv.VerticalOffset > 2);
    }

    private void SetTopBarSolid(bool solid)
    {
        if (solid == _topBarSolid) return;
        _topBarSolid = solid;
        TopBarBackground.BeginAnimation(OpacityProperty,
            new DoubleAnimation(solid ? 1 : 0, TimeSpan.FromMilliseconds(160)));
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) return hit;
            if (FindDescendant<T>(child) is { } deeper) return deeper;
        }
        return null;
    }

    // ============================================================ cor do tema

    public void SetAccent(Color? color)
    {
        var target = State.Data.Settings.UseGameColors ? color ?? DefaultAccent : DefaultAccent;
        if (_themeBrush.Color == target) return;
        var anim = new ColorAnimation(target, TimeSpan.FromMilliseconds(600))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        };
        _themeBrush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
        NativeMethods.SetCaptionColor(this, target);
    }

    // ============================================================ comandos

    private static Game? GameFrom(ExecutedRoutedEventArgs e) => e.Parameter as Game;

    private void OpenGame_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (GameFrom(e) is { } g) Navigate(new GamePage(g), null);
    }

    private void PlayGame_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (GameFrom(e) is { } g) State.Play(g);
    }

    private void ToggleFavorite_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (GameFrom(e) is { } g) State.ToggleFavorite(g);
    }

    private void OpenFolder_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (GameFrom(e) is { } g) State.OpenFolder(g);
    }

    private void OpenInStore_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (GameFrom(e) is { } g) State.OpenInStore(g);
    }

    // ============================================================ ecrã inteiro

    private void Fullscreen_Click(object sender, RoutedEventArgs e) => SetFullscreen(!_isFullscreen);

    public void SetFullscreen(bool on)
    {
        if (on == _isFullscreen) return;
        _isFullscreen = on;
        if (on)
        {
            _stateBeforeFullscreen = WindowState;
            WindowState = WindowState.Normal; // necessário para cobrir a barra de tarefas
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            WindowState = _stateBeforeFullscreen == WindowState.Minimized ? WindowState.Normal : _stateBeforeFullscreen;
        }
        FullscreenIcon.Text = on ? "" : "";
        FullscreenText.Text = Loc.T(on ? "ExitFullscreen" : "Fullscreen");
    }

    // ============================================================ teclado e comando

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (IsEchoOfPad(e.Key)) { e.Handled = true; return; }
        HideHints();
        _lastKey = e.Key;
        _lastKeyTime = DateTime.UtcNow;
        bool inText = Keyboard.FocusedElement is TextBox;

        switch (e.Key)
        {
            case Key.F11:
                SetFullscreen(!_isFullscreen); e.Handled = true; return;
            case Key.F5:
                _ = ReloadAsync(); e.Handled = true; return;
            case Key.Escape:
                if (inText) { FocusContent(); e.Handled = true; return; }
                if (_history.Count > 0) GoBack();
                else if (_isFullscreen) SetFullscreen(false);
                e.Handled = true; return;
            case Key.Back when !inText:
                GoBack(); e.Handled = true; return;
            case Key.F when Keyboard.Modifiers == ModifierKeys.Control:
                SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; return;
        }

        if (inText)
        {
            if (e.Key is Key.Up or Key.Down) { e.Handled = MoveFocus(e.Key == Key.Up ? FocusNavigationDirection.Up : FocusNavigationDirection.Down); }
            return; // dentro de uma caixa de texto as outras teclas escrevem
        }

        switch (e.Key)
        {
            case Key.Left: e.Handled = MoveFocus(FocusNavigationDirection.Left); break;
            case Key.Right: e.Handled = MoveFocus(FocusNavigationDirection.Right); break;
            case Key.Up: e.Handled = MoveFocus(FocusNavigationDirection.Up); break;
            case Key.Down: e.Handled = MoveFocus(FocusNavigationDirection.Down); break;
            case Key.Enter: e.Handled = ActivateFocused(); break;
        }
    }

    private bool IsEchoOfPad(Key key)
    {
        if (_lastPad is not { } pad || DateTime.UtcNow - _lastPadTime > TimeSpan.FromMilliseconds(150)) return false;
        return Matches(key, pad);
    }

    private bool IsEchoOfKey(PadButton pad)
    {
        if (_lastKey is not { } key || DateTime.UtcNow - _lastKeyTime > TimeSpan.FromMilliseconds(150)) return false;
        return Matches(key, pad);
    }

    private static bool Matches(Key key, PadButton pad)
    {
        return (key, pad) switch
        {
            (Key.Up, PadButton.Up) or (Key.Down, PadButton.Down) or (Key.Left, PadButton.Left)
                or (Key.Right, PadButton.Right) or (Key.Enter, PadButton.A) or (Key.Escape, PadButton.B) => true,
            _ => false,
        };
    }

    private void OnGamepad(PadButton b)
    {
        if (!IsActive) return; // não mexe na app enquanto estás dentro de um jogo
        if (IsEchoOfKey(b)) return;
        _lastPad = b;
        ShowHints();
        Dispatcher.BeginInvoke(UpdateHints, System.Windows.Threading.DispatcherPriority.Background);
        _lastPadTime = DateTime.UtcNow;

        switch (b)
        {
            case PadButton.Up: MoveFocus(FocusNavigationDirection.Up); break;
            case PadButton.Down: MoveFocus(FocusNavigationDirection.Down); break;
            case PadButton.Left: MoveFocus(FocusNavigationDirection.Left); break;
            case PadButton.Right: MoveFocus(FocusNavigationDirection.Right); break;
            case PadButton.A: ActivateFocused(); break;
            case PadButton.B:
                if (_history.Count > 0) GoBack(); else if (_isFullscreen) SetFullscreen(false);
                break;
            case PadButton.X:
                if (TargetGame() is { } toPlay) State.Play(toPlay);
                break;
            case PadButton.Y:
                if (TargetGame() is { } toFav) State.ToggleFavorite(toFav);
                break;
            case PadButton.Start: SetFullscreen(!_isFullscreen); break;
            case PadButton.Back:
            case PadButton.Guide: GoHome(); break;
            case PadButton.LB: CycleNav(-1); break;
            case PadButton.RB: CycleNav(+1); break;
        }
    }

    private bool MoveFocus(FocusNavigationDirection dir)
    {
        if (Keyboard.FocusedElement is not UIElement el || !IsAncestorOf(el))
        {
            FocusContent();
            return true;
        }
        return el.MoveFocus(new TraversalRequest(dir));
    }

    /// <summary>Põe o foco no primeiro elemento da página atual.</summary>
    public void FocusContent()
    {
        if (ContentHost.Content is UIElement page)
        {
            if (!page.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)))
                NavHome.Focus();
        }
    }

    private static bool ActivateFocused()
    {
        if (Keyboard.FocusedElement is not ButtonBase btn) return false;
        var peer = UIElementAutomationPeer.CreatePeerForElement(btn);
        switch (peer?.GetPattern(PatternInterface.Invoke))
        {
            case IInvokeProvider invoke: invoke.Invoke(); return true;
        }
        switch (peer?.GetPattern(PatternInterface.SelectionItem))
        {
            case ISelectionItemProvider sel: sel.Select(); btn.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); return true;
        }
        switch (peer?.GetPattern(PatternInterface.Toggle))
        {
            case IToggleProvider toggle: toggle.Toggle(); return true;
        }
        return false;
    }

    /// <summary>Jogo com foco (cartão) ou, se não houver, o jogo da página atual.</summary>
    private Game? TargetGame() =>
        (Keyboard.FocusedElement as FrameworkElement)?.DataContext as Game
        ?? (_current?.Page as IPage)?.ContextGame;

    private void CycleNav(int delta)
    {
        var items = AllNavItems().Where(r => r != NavSettings).ToList();
        var idx = items.FindIndex(r => r.IsChecked == true);
        var next = items[((idx < 0 ? 0 : idx + delta) % items.Count + items.Count) % items.Count];
        next.Focus();
        next.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    }

    public async Task ReloadAsync()
    {
        await State.LoadAsync();
        if (_current?.Page is IPage p) p.OnReturned();
    }
}
