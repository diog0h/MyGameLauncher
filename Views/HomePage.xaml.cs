using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GameLauncher.Localization;
using GameLauncher.Models;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class HomePage : UserControl, IPage
{
    public string Title => Loc.T("Nav_Home");
    public Game? ContextGame => _featured;

    private Game? _featured;
    private AppState State => AppState.Current;

    public HomePage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            State.LibraryChanged += BuildRows;
            State.GamesLoaded += Refresh;
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            State.LibraryChanged -= BuildRows;
            State.GamesLoaded -= Refresh;
            if (_featured is not null) _featured.PropertyChanged -= Featured_PropertyChanged;
        };
    }

    public void OnReturned() => Refresh();

    private void Refresh()
    {
        if (_featured is not null) _featured.PropertyChanged -= Featured_PropertyChanged;
        _featured = State.Featured;

        Hero.DataContext = _featured;
        Hero.Visibility = _featured is null ? Visibility.Collapsed : Visibility.Visible;
        EmptyText.Visibility = _featured is null ? Visibility.Visible : Visibility.Collapsed;

        if (_featured is not null) _featured.PropertyChanged += Featured_PropertyChanged;
        MainWindow.Current.SetAccent(_featured?.Accent);
        BuildRows();
    }

    private void Featured_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Game.Accent)) MainWindow.Current.SetAccent(_featured?.Accent);
    }

    private void BuildRows()
    {
        // Lembra o jogo com foco para o voltar a selecionar depois de reconstruir as filas.
        var focusedEl = Keyboard.FocusedElement as FrameworkElement;
        var focusedGame = focusedEl is not null && Rows.IsAncestorOf(focusedEl) ? focusedEl.DataContext as Game : null;
        int focusedRow = focusedGame is null ? -1 : Rows.Children.IndexOf(Rows.Children.OfType<FrameworkElement>()
            .FirstOrDefault(r => r.IsAncestorOf(focusedEl!)));

        BuildRowsCore();

        if (focusedGame is not null)
        {
            Rows.UpdateLayout();
            var rows = Rows.Children.OfType<FrameworkElement>().ToList();
            var ordered = focusedRow >= 0 && focusedRow < rows.Count
                ? rows.Skip(focusedRow).Concat(rows.Take(focusedRow)) : rows;
            var target = ordered.SelectMany(FindButtons).FirstOrDefault(b => b.DataContext == focusedGame);
            target?.Focus();
        }
    }

    private static IEnumerable<Button> FindButtons(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Button b) yield return b;
            foreach (var inner in FindButtons(child)) yield return inner;
        }
    }

    private void BuildRowsCore()
    {
        Rows.Children.Clear();
        var recent = State.RecentGames.Where(g => g.FullyInstalled).Take(20).ToList();
        if (recent.Count > 0)
            Rows.Children.Add(BuildRow(Loc.T("Home_ReadyToPlay"), recent, () => MainWindow.Current.OpenLibrary(LibraryMode.All)));

        var favs = State.RecentGames.Where(g => g.IsFavorite).ToList();
        if (favs.Count > 0)
            Rows.Children.Add(BuildRow(Loc.T("Nav_Favorites"), favs, () => MainWindow.Current.OpenLibrary(LibraryMode.Favorites)));

        foreach (var c in State.Data.Collections)
        {
            var games = State.GamesIn(c).ToList();
            if (games.Count > 0)
                Rows.Children.Add(BuildRow(c.Name, games, () => MainWindow.Current.OpenLibrary(LibraryMode.Collection, c)));
        }
    }

    private UIElement BuildRow(string title, List<Game> games, Action seeAll)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };

        var header = new DockPanel { Margin = new Thickness(44, 0, 36, 0) };
        var link = new Button { Content = Loc.T("SeeAll"), Style = (Style)FindResource("LinkButton"), VerticalAlignment = VerticalAlignment.Center };
        link.Click += (_, _) => seeAll();
        DockPanel.SetDock(link, Dock.Right);
        header.Children.Add(link);

        // Setas para deslizar a fila (não recebem foco: no comando/teclado a fila desliza sozinha com a seleção).
        Button Arrow(string glyph) => new()
        {
            Content = glyph, Style = (Style)FindResource("RoundIconButton"),
            Width = 34, Height = 34, FontSize = 12, Focusable = false, Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var right = Arrow("\uE76C");
        var left = Arrow("\uE76B");
        right.Margin = new Thickness(6, 0, 12, 0);
        DockPanel.SetDock(right, Dock.Right);
        DockPanel.SetDock(left, Dock.Right);
        header.Children.Add(right);
        header.Children.Add(left);
        header.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("SectionTitle") });
        panel.Children.Add(header);

        var items = new ItemsControl
        {
            ItemsSource = games,
            ItemTemplate = (DataTemplate)FindResource("CoverCardTemplate"),
            ItemsPanel = HorizontalPanel,
            Focusable = false,
            Margin = new Thickness(44, 16, 44, 0),
        };

        var scroller = new ScrollViewer
        {
            Content = items,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
            Padding = new Thickness(0, 0, 0, 6),
        };
        ScrollHelper.ForwardWheelToParent(scroller);

        left.Click += (_, _) => ScrollHelper.AnimateHorizontal(scroller, scroller.HorizontalOffset - scroller.ViewportWidth * 0.8);
        right.Click += (_, _) => ScrollHelper.AnimateHorizontal(scroller, scroller.HorizontalOffset + scroller.ViewportWidth * 0.8);
        void UpdateArrows()
        {
            bool scrollable = scroller.ScrollableWidth > 1;
            left.Visibility = right.Visibility = scrollable ? Visibility.Visible : Visibility.Collapsed;
            left.IsEnabled = scroller.HorizontalOffset > 1;
            right.IsEnabled = scroller.HorizontalOffset < scroller.ScrollableWidth - 1;
            left.Opacity = left.IsEnabled ? 1 : 0.35;
            right.Opacity = right.IsEnabled ? 1 : 0.35;
        }
        scroller.ScrollChanged += (_, _) => UpdateArrows();
        panel.Children.Add(scroller);
        return panel;
    }

    private static readonly ItemsPanelTemplate HorizontalPanel = CreateHorizontalPanel();

    private static ItemsPanelTemplate CreateHorizontalPanel()
    {
        var f = new FrameworkElementFactory(typeof(StackPanel));
        f.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        var t = new ItemsPanelTemplate(f);
        t.Seal();
        return t;
    }

    /// <summary>
    /// A arte ocupa sempre a largura toda com a proporção certa (≈ 3,1:1), por isso nunca é cortada dos lados.
    /// A zona do banner tem uma altura mínima para caber o logótipo e os botões por baixo da arte.
    /// </summary>
    private void Banner_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged || sender is not FrameworkElement banner) return;
        double art = Math.Min(banner.ActualWidth / 3.1, 640);
        BannerArt.Height = art;
        var h = Math.Max(art, 400);
        if (Math.Abs(banner.Height - h) > 1) banner.Height = h;
    }
}
