using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GameLauncher.Localization;
using GameLauncher.Models;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class GamePage : UserControl, IPage
{
    public string Title => "";
    public Game? ContextGame => _game;

    private Game _game;
    private AppState State => AppState.Current;

    public GamePage(Game game)
    {
        _game = game;
        InitializeComponent();
        DataContext = game;

        Loaded += (_, _) =>
        {
            // Depois de "Atualizar", o objeto do jogo é novo: vai buscar o atual.
            if (State.FindGame(_game.Id) is { } fresh && fresh != _game) { _game = fresh; DataContext = fresh; }

            _game.PropertyChanged += Game_PropertyChanged;
            State.LibraryChanged += BuildCollections;
            State.SessionsChanged += BuildSessions;
            MainWindow.Current.SetAccent(_game.Accent);
            BuildCollections();
            BuildSessions();
        };
        Unloaded += (_, _) =>
        {
            _game.PropertyChanged -= Game_PropertyChanged;
            State.LibraryChanged -= BuildCollections;
            State.SessionsChanged -= BuildSessions;
        };
    }

    private void Game_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Game.Accent)) MainWindow.Current.SetAccent(_game.Accent);
    }

    private void StopGame_Click(object sender, RoutedEventArgs e)
    {
        var ok = MessageBox.Show(Loc.F("Game_StopConfirm", _game.Name), Loc.T("Game_Stop"),
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ok == MessageBoxResult.Yes) State.StopGame(_game);
    }

    private void Customize_Click(object sender, RoutedEventArgs e) =>
        MainWindow.Current.Navigate(new ArtworkPage(_game));

    // ------------------------------------------------------------ remover (jogos adicionados à mão)

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (!_game.IsCustom) return;
        var ok = MessageBox.Show(Loc.F("Game_RemoveConfirm", _game.Name), Loc.T("Game_Remove"),
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (ok != MessageBoxResult.Yes) return;
        await State.RemoveCustomGameAsync(_game);
        MainWindow.Current.GoHome();
    }

    // ------------------------------------------------------------ coleções

    private void BuildCollections()
    {
        CollectionChips.Children.Clear();
        foreach (var c in State.Data.Collections)
        {
            var chip = new CheckBox
            {
                Content = c.Name,
                Style = (Style)FindResource("ChipToggle"),
                IsChecked = c.GameIds.Contains(_game.Id),
                Tag = c,
            };
            chip.Checked += Chip_Changed;
            chip.Unchecked += Chip_Changed;
            CollectionChips.Children.Add(chip);
        }
        if (State.Data.Collections.Count == 0)
            CollectionChips.Children.Add(new TextBlock
            {
                Text = Loc.T("Game_NoCollections"),
                Foreground = (System.Windows.Media.Brush)FindResource("SubtleTextBrush"),
                Margin = new Thickness(0, 0, 0, 10),
            });
    }

    private void Chip_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: GameCollection c } cb)
        {
            // Atualiza os dados sem reconstruir os botões (para não perder o foco do comando).
            State.LibraryChanged -= BuildCollections;
            State.SetInCollection(c, _game, cb.IsChecked == true);
            State.LibraryChanged += BuildCollections;
        }
    }

    private void AddCollection_Click(object sender, RoutedEventArgs e) => CreateAndAdd();

    private void NewCollectionBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { CreateAndAdd(); e.Handled = true; }
    }

    private void CreateAndAdd()
    {
        var c = State.CreateCollection(NewCollectionBox.Text);
        if (c is null) { NewCollectionBox.Focus(); return; }
        State.SetInCollection(c, _game, true);
        NewCollectionBox.Text = "";
    }

    // ------------------------------------------------------------ sessões

    private sealed record SessionRow(string When, string Duration);

    private void BuildSessions()
    {
        var rows = State.Data.Sessions
            .Where(s => s.GameId == _game.Id)
            .OrderByDescending(s => s.Start)
            .Take(8)
            .Select(s => new SessionRow(
                s.Start.ToString("dddd, d MMM · HH:mm", Loc.Culture),
                Game.FormatMinutes((int)Math.Round(s.Seconds / 60))))
            .ToList();

        SessionsList.ItemsSource = rows;
        NoSessionsText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
