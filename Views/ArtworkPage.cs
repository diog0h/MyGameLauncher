using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GameLauncher.Localization;
using GameLauncher.Models;
using GameLauncher.Services;
using Microsoft.Win32;

namespace GameLauncher.Views;

/// <summary>
/// "Personalizar" (aberta a partir da página do jogo): as imagens do jogo e,
/// nos jogos adicionados à mão, a pasta principal (usada para o tamanho e para saber quando está aberto).
/// </summary>
public sealed class ArtworkPage : UserControl, IPage
{
    private Game _game;
    private readonly ArtworkEditor _editor = new();
    private readonly TextBlock _heading = new() { FontSize = 34, FontWeight = FontWeights.Bold };
    private readonly TextBlock _desc = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 22) };
    private readonly ContentControl _folderHost = new() { Focusable = false };
    private readonly TextBlock _imagesTitle = new() { FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 12) };
    private bool _busy;

    private AppState State => AppState.Current;

    public string Title => Loc.T("Art_PageTitle");
    public Game? ContextGame => _game;

    public ArtworkPage(Game game)
    {
        _game = game;
        _desc.Foreground = (Brush)Application.Current.FindResource("SubtleTextBrush");
        _editor.Bind(game);

        var panel = new StackPanel { Margin = new Thickness(44, 76, 40, 40), MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(_heading);
        panel.Children.Add(_desc);
        panel.Children.Add(_folderHost);
        panel.Children.Add(_imagesTitle);
        panel.Children.Add(_editor);
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Content = panel };

        Loaded += (_, _) =>
        {
            Loc.Changed += UpdateAll;
            State.GamesLoaded += OnGamesLoaded;
            _game.PropertyChanged += Game_PropertyChanged;
            UpdateAll();
        };
        Unloaded += (_, _) =>
        {
            Loc.Changed -= UpdateAll;
            State.GamesLoaded -= OnGamesLoaded;
            _game.PropertyChanged -= Game_PropertyChanged;
        };
    }

    /// <summary>Depois de mudar a pasta a lista de jogos é recarregada: passa a usar o objeto novo.</summary>
    private void OnGamesLoaded()
    {
        if (State.FindGame(_game.Id) is not { } fresh || fresh == _game) return;
        _game.PropertyChanged -= Game_PropertyChanged;
        _game = fresh;
        _game.PropertyChanged += Game_PropertyChanged;
        _editor.Bind(fresh);
        UpdateAll();
    }

    private void Game_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Game.SizeBytes)) Dispatcher.BeginInvoke(BuildFolderCard);
    }

    private void UpdateAll()
    {
        _heading.Text = _game.Name;
        _desc.Text = Loc.T("Art_PageDesc");
        _imagesTitle.Text = Loc.T("Art_Images");
        _imagesTitle.Visibility = _game.IsCustom ? Visibility.Visible : Visibility.Collapsed;
        BuildFolderCard();
    }

    // ------------------------------------------------------------ pasta do jogo (só jogos adicionados à mão)

    private CustomGame? Custom => State.Data.CustomGames.FirstOrDefault(c => c.Id == _game.Id);

    private void BuildFolderCard()
    {
        if (!_game.IsCustom || Custom is not { } custom) { _folderHost.Content = null; return; }

        var subtle = (Brush)FindResource("SubtleTextBrush");
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel { Margin = new Thickness(0, 0, 18, 0) };
        info.Children.Add(new TextBlock { Text = Loc.T("Add_Folder"), FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        info.Children.Add(new TextBlock
        {
            Text = _game.InstallPath ?? "—",
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12.5,
            Margin = new Thickness(0, 0, 0, 6),
        });
        info.Children.Add(new TextBlock
        {
            Text = _game.SizeBytes > 0 ? Loc.F("Folder_Size", _game.SizeText) : Loc.T("Folder_Measuring"),
            Foreground = subtle,
        });
        info.Children.Add(new TextBlock
        {
            Text = Loc.T("Add_FolderDesc"),
            Foreground = subtle,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12.5,
            Margin = new Thickness(0, 10, 0, 0),
        });
        grid.Children.Add(info);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        var change = MakeButton("", Loc.T("Art_Change"));
        change.Click += async (_, _) => await ChangeFolderAsync();
        var reset = MakeButton("", Loc.T("Art_Reset"));
        reset.Margin = new Thickness(10, 0, 0, 0);
        reset.IsEnabled = custom.Folder is not null && !_busy;
        change.IsEnabled = !_busy;
        reset.Click += async (_, _) => await SetFolderAsync(null);
        buttons.Children.Add(change);
        buttons.Children.Add(reset);
        Grid.SetColumn(buttons, 1);
        grid.Children.Add(buttons);

        _folderHost.Content = new Border
        {
            Background = (Brush)FindResource("SurfaceBrush"),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(22, 18, 22, 18),
            Margin = new Thickness(0, 0, 0, 12),
            Child = grid,
        };
    }

    private Button MakeButton(string icon, string text)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock { Text = icon, Style = (Style)FindResource("Icon"), Margin = new Thickness(0, 0, 8, 0), FontSize = 13 });
        content.Children.Add(new TextBlock { Text = text });
        return new Button { Style = (Style)FindResource("GlassButton"), Padding = new Thickness(16, 8, 16, 8), Content = content };
    }

    private async Task ChangeFolderAsync()
    {
        var dlg = new OpenFolderDialog { Title = Loc.T("Add_Folder") };
        if (_game.InstallPath is { } current && Directory.Exists(current))
            dlg.InitialDirectory = Directory.GetParent(current)?.FullName ?? current;
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        await SetFolderAsync(dlg.FolderName);
    }

    private async Task SetFolderAsync(string? folder)
    {
        if (_busy) return;
        _busy = true;
        BuildFolderCard();
        try { await State.SetCustomFolderAsync(_game, folder); }
        finally { _busy = false; BuildFolderCard(); }
    }
}
