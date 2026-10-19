using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using GameLauncher.Helpers;
using GameLauncher.Localization;
using GameLauncher.Models;
using GameLauncher.Services;
using Microsoft.Win32;

namespace GameLauncher.Views;

/// <summary>
/// As quatro imagens de um jogo (capa, plano de fundo, logótipo e capa ampla), cada uma com "Mudar" e "Repor".
/// Dois modos:
///  - com um jogo (<see cref="Bind"/>): as mudanças aplicam-se logo;
///  - sem jogo (página "Adicionar jogo"): as escolhas ficam em <see cref="Pending"/> até o jogo ser criado.
/// </summary>
public sealed class ArtworkEditor : UserControl
{
    private sealed record Slot(ArtKind Kind, string TitleKey, string DescKey, double Width, double Height);

    private static readonly Slot[] Slots =
    {
        new(ArtKind.Cover, "Art_Cover", "Art_CoverDesc", 150, 225),
        new(ArtKind.Hero, "Art_Hero", "Art_HeroDesc", 220, 71),
        new(ArtKind.Logo, "Art_Logo", "Art_LogoDesc", 220, 96),
        new(ArtKind.Header, "Art_Header", "Art_HeaderDesc", 220, 103),
    };

    private readonly StackPanel _rows = new();
    private readonly TextBlock _error = new()
    {
        Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xB4, 0xB4)),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 12),
        Visibility = Visibility.Collapsed,
    };
    private Game? _game;
    private bool _busy;

    /// <summary>Imagens escolhidas antes de o jogo existir (ficheiros originais).</summary>
    public Dictionary<ArtKind, string> Pending { get; } = new();

    private AppState State => AppState.Current;

    public ArtworkEditor()
    {
        Focusable = false;
        var root = new StackPanel();
        root.Children.Add(_error);
        root.Children.Add(_rows);
        Content = root;

        Loaded += (_, _) =>
        {
            Loc.Changed += Build;
            if (_game is not null) _game.PropertyChanged += Game_PropertyChanged;
            Build();
        };
        Unloaded += (_, _) =>
        {
            Loc.Changed -= Build;
            if (_game is not null) _game.PropertyChanged -= Game_PropertyChanged;
        };
    }

    public void Bind(Game game)
    {
        if (_game is not null) _game.PropertyChanged -= Game_PropertyChanged;
        _game = game;
        if (IsLoaded) { _game.PropertyChanged += Game_PropertyChanged; Build(); }
    }

    private void Game_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Game.CoverPath) or nameof(Game.HeroPath) or nameof(Game.LogoPath) or nameof(Game.HeaderPath))
            Dispatcher.BeginInvoke(Build);
    }

    // ------------------------------------------------------------ estado de cada imagem

    private string? CurrentPath(ArtKind kind)
    {
        if (_game is null) return Pending.GetValueOrDefault(kind);
        return kind switch
        {
            ArtKind.Cover => _game.CoverPath,
            ArtKind.Hero => _game.HeroPath,
            ArtKind.Logo => _game.LogoPath,
            _ => _game.HeaderPath,
        };
    }

    private bool IsCustom(ArtKind kind) =>
        _game is null ? Pending.ContainsKey(kind) : State.HasCustomArt(_game, kind);

    // ------------------------------------------------------------ desenho

    private void Build()
    {
        // Mantém o foco no mesmo botão depois de redesenhar (útil com o comando).
        var focused = (Keyboard.FocusedElement as FrameworkElement)?.Tag as string;

        _rows.Children.Clear();
        foreach (var slot in Slots) _rows.Children.Add(BuildRow(slot));

        if (focused is not null)
            foreach (var b in FindButtons(_rows))
                if (b.Tag as string == focused) { b.Focus(); break; }
    }

    private Border BuildRow(Slot slot)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Título (+ etiqueta "escolhida por ti")
        var title = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        title.Children.Add(new TextBlock { Text = Loc.T(slot.TitleKey), FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        if (IsCustom(slot.Kind))
        {
            title.Children.Add(new Border
            {
                Background = (Brush)FindResource("GlassBrush"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(9, 2, 9, 3),
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = Loc.T("Art_Custom"), FontSize = 11.5, Foreground = (Brush)FindResource("SubtleTextBrush") },
            });
        }
        Grid.SetColumnSpan(title, 2);
        grid.Children.Add(title);

        // Pré-visualização
        var preview = BuildPreview(slot);
        Grid.SetRow(preview, 1);
        grid.Children.Add(preview);

        // Descrição
        var desc = new TextBlock
        {
            Text = Loc.T(slot.DescKey),
            Foreground = (Brush)FindResource("SubtleTextBrush"),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 18, 0),
            LineHeight = 20,
        };
        Grid.SetRow(desc, 1);
        Grid.SetColumn(desc, 1);
        grid.Children.Add(desc);

        // Botões
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        var change = MakeButton("", Loc.T("Art_Change"), slot.Kind + ":change");
        change.Click += async (_, _) => await ChangeAsync(slot.Kind);
        var reset = MakeButton("", Loc.T("Art_Reset"), slot.Kind + ":reset");
        reset.Margin = new Thickness(10, 0, 0, 0);
        reset.IsEnabled = IsCustom(slot.Kind) && !_busy;
        change.IsEnabled = !_busy;
        reset.Click += async (_, _) => await ResetAsync(slot.Kind);
        buttons.Children.Add(change);
        buttons.Children.Add(reset);
        Grid.SetColumn(buttons, 2);
        Grid.SetRowSpan(buttons, 2);
        grid.Children.Add(buttons);

        return new Border
        {
            Background = (Brush)FindResource("SurfaceBrush"),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(22, 18, 22, 18),
            Margin = new Thickness(0, 0, 0, 12),
            Child = grid,
        };
    }

    private FrameworkElement BuildPreview(Slot slot)
    {
        var path = CurrentPath(slot.Kind);
        var image = path is null ? null : ImageHelper.Load(path, (int)(slot.Width * 2));
        var host = new Grid { Width = slot.Width, Height = slot.Height, HorizontalAlignment = HorizontalAlignment.Left };

        if (image is null)
        {
            // Sem imagem: moldura tracejada com um ícone.
            host.Children.Add(new Rectangle
            {
                RadiusX = 8, RadiusY = 8,
                Stroke = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Fill = (Brush)FindResource("SurfaceBrush"),
            });
            var empty = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            empty.Children.Add(new TextBlock
            {
                Text = "", Style = (Style)FindResource("Icon"), FontSize = slot.Height > 100 ? 26 : 18,
                HorizontalAlignment = HorizontalAlignment.Center, Foreground = (Brush)FindResource("FaintTextBrush"),
            });
            empty.Children.Add(new TextBlock
            {
                Text = Loc.T(_game is null ? "Art_Auto" : "Art_None"), FontSize = 12, Margin = new Thickness(0, 4, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center, Foreground = (Brush)FindResource("FaintTextBrush"),
            });
            host.Children.Add(empty);
            return host;
        }

        if (slot.Kind == ArtKind.Logo)
        {
            // O logótipo costuma ter fundo transparente: mostra-o sobre um fundo escuro, sem cortar.
            host.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(0x59, 0, 0, 0)),
                Padding = new Thickness(10),
                Child = new Image { Source = image, Stretch = Stretch.Uniform },
            });
            return host;
        }

        host.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill },
        });
        return host;
    }

    private Button MakeButton(string icon, string text, string tag)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock { Text = icon, Style = (Style)FindResource("Icon"), Margin = new Thickness(0, 0, 8, 0), FontSize = 13 });
        content.Children.Add(new TextBlock { Text = text });
        return new Button
        {
            Style = (Style)FindResource("GlassButton"),
            Padding = new Thickness(16, 8, 16, 8),
            Content = content,
            Tag = tag,
        };
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

    // ------------------------------------------------------------ ações

    private async Task ChangeAsync(ArtKind kind)
    {
        var dlg = new OpenFileDialog
        {
            Title = Loc.T(Slots.First(s => s.Kind == kind).TitleKey),
            Filter = $"{Loc.T("Art_FileFilter")}|{CustomArtStore.FileFilterPattern}|{Loc.T("Add_AllFiles")}|*.*",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;

        if (!CustomArtStore.IsReadableImage(dlg.FileName))
        {
            ShowError(Loc.T("Art_ErrImage"));
            return;
        }
        ShowError(null);

        if (_game is null)
        {
            Pending[kind] = dlg.FileName;
            Build();
            return;
        }
        await RunAsync(() => State.SetCustomArtAsync(_game, kind, dlg.FileName));
    }

    private async Task ResetAsync(ArtKind kind)
    {
        ShowError(null);
        if (_game is null)
        {
            Pending.Remove(kind);
            Build();
            return;
        }
        await RunAsync(() => State.SetCustomArtAsync(_game, kind, null));
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        Build();
        try { await action(); }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { _busy = false; Build(); }
    }

    private void ShowError(string? text)
    {
        _error.Text = text ?? "";
        _error.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
    }
}
