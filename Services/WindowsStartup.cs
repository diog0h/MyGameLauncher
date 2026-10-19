using Microsoft.Win32;

namespace GameLauncher.Services;

/// <summary>Abrir a app com o Windows (escondida na área de notificação), através da chave "Run" do utilizador.</summary>
public static class WindowsStartup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MyGameLauncher";
    public const string TrayArgument = "--tray";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) is string;
            }
            catch { return false; }
        }
    }

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled && Environment.ProcessPath is { } exe)
                key.SetValue(ValueName, $"\"{exe}\" {TrayArgument}");
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception ex) { AppLog.Error("Não foi possível mudar o arranque com o Windows", ex); }
    }

    /// <summary>Se a app mudou de sítio (ex.: reinstalada noutra pasta), atualiza o caminho guardado.</summary>
    public static void RefreshPath()
    {
        if (IsEnabled) Set(true);
    }
}
