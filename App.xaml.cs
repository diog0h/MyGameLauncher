using System.Windows;
using GameLauncher.Localization;

namespace GameLauncher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Mostra erros inesperados em vez de a app fechar sem aviso.
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.ToString(), Loc.T("Err_Unexpected"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        // Escolhe a língua antes de a janela ser criada.
        Loc.SetLanguage(GameLauncher.Services.AppState.Current.Data.Settings.Language);
        base.OnStartup(e);
    }
}
