using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using GameLauncher.Localization;
using GameLauncher.Models;
using GameLauncher.Services;
using Microsoft.Win32;

namespace GameLauncher.Views;

/// <summary>Adicionar qualquer jogo ou launcher à mão: um .exe, um atalho (.lnk) ou um link (.url).</summary>
public partial class AddGamePage : UserControl, IPage
{
    public string Title => Loc.T("Add_Title");

    private AppState State => AppState.Current;
    private bool _nameTouched;

    /// <summary>Sugestões de nomes de loja/launcher, além dos que já foram usados.</summary>
    private static readonly string[] SuggestedStores = { "Battle.net", "EA app", "GOG", "Riot Games", "Rockstar", "Xbox" };

    public AddGamePage()
    {
        InitializeComponent();
        NameBox.TextChanged += (_, _) => { if (NameBox.IsKeyboardFocused) _nameTouched = true; };
        BuildStoreChips();
    }

    private void BuildStoreChips()
    {
        var used = State.Data.CustomGames.Select(c => c.Store);
        var names = used.Concat(SuggestedStores).Append(Loc.T("Add_Other"))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.CurrentCultureIgnoreCase);

        foreach (var name in names)
        {
            var chip = new Button
            {
                Content = name,
                Style = (Style)FindResource("GlassButton"),
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 8, 8),
            };
            chip.Click += (_, _) => StoreBox.Text = name;
            StoreChips.Children.Add(chip);
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = Loc.T("Add_Title"),
            Filter = $"{Loc.T("Add_FileFilter")}|*.exe;*.lnk;*.url|{Loc.T("Add_AllFiles")}|*.*",
            CheckFileExists = true,
            DereferenceLinks = false, // guarda o atalho em si (mantém argumentos do atalho)
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) == true) PathBox.Text = dlg.FileName;
    }

    private void PathBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_nameTouched) return;
        NameBox.Text = GuessName(PathBox.Text.Trim().Trim('"'));
    }

    /// <summary>Nome do jogo a partir do ficheiro: descrição do .exe se existir, senão o nome do ficheiro.</summary>
    private static string GuessName(string path)
    {
        if (path.Length == 0) return "";
        try
        {
            var target = Scanners.CustomGames.ResolveTarget(path) ?? path;
            if (target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(target))
            {
                var info = FileVersionInfo.GetVersionInfo(target);
                var desc = info.FileDescription?.Trim();
                if (!string.IsNullOrEmpty(desc) && desc.Length > 2 && !desc.Contains("Launcher", StringComparison.OrdinalIgnoreCase))
                    return desc;
                if (!string.IsNullOrWhiteSpace(info.ProductName)) return info.ProductName.Trim();
            }
        }
        catch { }
        return Path.GetFileNameWithoutExtension(path);
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var path = PathBox.Text.Trim().Trim('"');
        var name = NameBox.Text.Trim();
        var store = StoreBox.Text.Trim();
        if (store.Length == 0) store = Loc.T("Add_Other");

        string? error = null;
        if (path.Length == 0 || (!File.Exists(path) && !path.Contains("://"))) error = Loc.T("Add_ErrFile");
        else if (name.Length == 0) error = Loc.T("Add_ErrName");

        ErrorText.Text = error ?? "";
        ErrorText.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
        if (error is not null) return;

        var game = await State.AddCustomGameAsync(new CustomGame
        {
            Name = name,
            Store = store,
            Path = path,
            Arguments = string.IsNullOrWhiteSpace(ArgsBox.Text) ? null : ArgsBox.Text.Trim(),
        });
        if (game is not null) MainWindow.Current.Navigate(new GamePage(game), null, addToHistory: false);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => MainWindow.Current.GoBack();
}
