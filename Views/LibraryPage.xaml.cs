using System.Windows;
using System.Windows.Controls;
using GameLauncher.Localization;
using GameLauncher.Models;
using GameLauncher.Services;

namespace GameLauncher.Views;

public enum LibraryMode { All, Favorites, Collection, Store }

public partial class LibraryPage : UserControl, IPage
{
    public LibraryMode Mode { get; }
    public GameCollection? Collection { get; }
    public string? StoreName { get; }

    public string Title => Mode switch
    {
        LibraryMode.Favorites => Loc.T("Nav_Favorites"),
        LibraryMode.Collection => Collection?.Name ?? Loc.T("Lib_Collection"),
        LibraryMode.Store => StoreName ?? "",
        _ => Loc.T("Nav_AllGames"),
    };

    private string _query = "";
    public string Query
    {
        get => _query;
        set { _query = value ?? ""; if (IsLoaded) Refresh(); }
    }

    private AppState State => AppState.Current;
    private bool _ready;

    public LibraryPage(LibraryMode mode, GameCollection? collection = null, string? store = null)
    {
        Mode = mode;
        Collection = collection;
        StoreName = store;
        InitializeComponent();

        HeaderText.Text = Title;
        DeleteCollectionButton.Visibility = mode == LibraryMode.Collection ? Visibility.Visible : Visibility.Collapsed;

        var sort = State.Data.Settings.LibrarySort;
        (sort switch { "name" => SortName, "playtime" => SortPlaytime, "size" => SortSize, _ => SortRecent }).IsChecked = true;
        _ready = true;

        Loaded += (_, _) =>
        {
            State.LibraryChanged += Refresh;
            State.GamesLoaded += Refresh;
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            State.LibraryChanged -= Refresh;
            State.GamesLoaded -= Refresh;
        };
    }

    public void OnReturned() => Refresh();

    private void Refresh()
    {
        IEnumerable<Game> games = Mode switch
        {
            LibraryMode.Favorites => State.Games.Where(g => g.IsFavorite),
            LibraryMode.Collection when Collection is not null => State.GamesIn(Collection),
            LibraryMode.Store => State.Games.Where(g => g.Store == StoreName),
            _ => State.Games,
        };

        if (_query.Length > 0)
            games = games.Where(g => g.Name.Contains(_query, StringComparison.CurrentCultureIgnoreCase));

        games = State.Data.Settings.LibrarySort switch
        {
            "name" => games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase),
            "playtime" => games.OrderByDescending(g => g.TotalPlaytimeMinutes),
            "size" => games.OrderByDescending(g => g.SizeBytes),
            _ => games.OrderByDescending(g => g.LastPlayed ?? DateTime.MinValue),
        };

        var list = games.ToList();
        GamesGrid.ItemsSource = list;

        CountText.Text = list.Count == 1 ? Loc.T("Lib_OneGame") : Loc.F("Lib_NGames", list.Count);
        if (_query.Length > 0) CountText.Text += Loc.F("Lib_ForQuery", _query);

        EmptyText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = _query.Length > 0 ? Loc.T("Lib_NoMatch")
            : Mode switch
            {
                LibraryMode.Favorites => Loc.T("Lib_NoFavorites"),
                LibraryMode.Collection => Loc.T("Lib_EmptyCollection"),
                _ => Loc.T("Lib_NoGames"),
            };
    }

    private void Sort_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready || (sender as FrameworkElement)?.Tag is not string key) return;
        State.Data.Settings.LibrarySort = key;
        State.Save();
        Refresh();
    }

    private void DeleteCollection_Click(object sender, RoutedEventArgs e)
    {
        if (Collection is null) return;
        var ok = MessageBox.Show(Loc.F("Lib_DeleteConfirm", Collection.Name),
            Loc.T("Lib_DeleteCollection"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (ok != MessageBoxResult.Yes) return;
        State.DeleteCollection(Collection);
        MainWindow.Current.GoHome();
    }
}
