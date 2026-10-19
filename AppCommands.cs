using System.Windows.Input;

namespace GameLauncher;

/// <summary>Comandos usados pelos cartões e botões em qualquer página (tratados na MainWindow).</summary>
public static class AppCommands
{
    public static readonly RoutedUICommand OpenGame = new("Open game", nameof(OpenGame), typeof(AppCommands));
    public static readonly RoutedUICommand PlayGame = new("Play", nameof(PlayGame), typeof(AppCommands));
    public static readonly RoutedUICommand ToggleFavorite = new("Favorite", nameof(ToggleFavorite), typeof(AppCommands));
    public static readonly RoutedUICommand OpenFolder = new("Folder", nameof(OpenFolder), typeof(AppCommands));
    public static readonly RoutedUICommand OpenInStore = new("Steam", nameof(OpenInStore), typeof(AppCommands));
}
