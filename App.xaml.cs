using System.Windows;
using GameLauncher.Localization;
using GameLauncher.Services;

namespace GameLauncher;

public partial class App : Application
{
    // Só pode haver uma MyGameLauncher aberta: uma segunda cópia pede à primeira para aparecer e fecha.
    private const string MutexName = @"Local\MyGameLauncher.diog0_h";
    private const string ShowEventName = @"Local\MyGameLauncher.diog0_h.Show";
    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private bool _showingError;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out bool first);
        if (!first)
        {
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
            Shutdown();
            return;
        }

        // Erros inesperados: ficam no ficheiro de registo e aparece uma mensagem simples, em vez de a app fechar.
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("Erro inesperado", args.Exception);
            args.Handled = true;
            if (_showingError) return; // não empilha mensagens se o mesmo erro se repetir
            _showingError = true;
            var answer = MessageBox.Show(Loc.T("Err_Friendly"), "MyGameLauncher", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            _showingError = false;
            if (answer == MessageBoxResult.Yes) AppLog.OpenFolder();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) AppLog.Error("Erro fatal", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Erro numa tarefa em segundo plano", args.Exception);
            args.SetObserved();
        };

        // Ao desligar o PC ou terminar sessão, fecha mesmo (mesmo com "continuar na área de notificação").
        SessionEnding += (_, _) => (MainWindow as MainWindow)?.ExitApp();

        // Escolhe a língua antes de a janela ser criada.
        Loc.SetLanguage(AppState.Current.Data.Settings.Language);
        AppLog.Info($"Arranque da versão {UpdateChecker.CurrentVersionText} ({Environment.OSVersion})");
        WindowsStartup.RefreshPath();
        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;
        if (e.Args.Contains(WindowsStartup.TrayArgument, StringComparer.OrdinalIgnoreCase))
            window.StartInTray();   // aberta com o Windows: fica só na área de notificação
        else
            window.Show();

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var listener = new Thread(() =>
        {
            while (_showEvent.WaitOne())
                Dispatcher.BeginInvoke(() => (MainWindow as MainWindow)?.ShowFromTray());
        })
        { IsBackground = true, Name = "Show request" };
        listener.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _mutex?.ReleaseMutex(); } catch { }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
