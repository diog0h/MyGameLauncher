using System.Windows;
using System.Windows.Controls;
using GameLauncher.Localization;
using GameLauncher.Models;

namespace GameLauncher.Views;

/// <summary>"Personalizar": escolher as imagens de um jogo (aberta a partir da página do jogo).</summary>
public sealed class ArtworkPage : UserControl, IPage
{
    private readonly Game _game;
    private readonly TextBlock _heading = new() { FontSize = 34, FontWeight = FontWeights.Bold };
    private readonly TextBlock _desc = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 22) };

    public string Title => Loc.T("Art_PageTitle");
    public Game? ContextGame => _game;

    public ArtworkPage(Game game)
    {
        _game = game;
        _desc.Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("SubtleTextBrush");

        var editor = new ArtworkEditor();
        editor.Bind(game);

        var panel = new StackPanel { Margin = new Thickness(44, 76, 40, 40), MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(_heading);
        panel.Children.Add(_desc);
        panel.Children.Add(editor);
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Content = panel };

        Loaded += (_, _) => { Loc.Changed += UpdateTexts; UpdateTexts(); };
        Unloaded += (_, _) => Loc.Changed -= UpdateTexts;
    }

    private void UpdateTexts()
    {
        _heading.Text = _game.Name;
        _desc.Text = Loc.T("Art_PageDesc");
    }
}
