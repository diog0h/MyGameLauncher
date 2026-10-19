using System.Text;

namespace GameLauncher.Services;

/// <summary>
/// Ficheiro de registo em %LocalAppData%\GameLauncher\logs\MyGameLauncher.log.
/// Guarda erros e o resumo de cada arranque, para se perceber o que correu mal sem ter de reproduzir o problema.
/// Quando passa de 1 MB, o ficheiro antigo passa a .old.log (fica só um).
/// </summary>
public static class AppLog
{
    public static string Folder => Path.Combine(DataStore.Root, "logs");
    public static string FilePath => Path.Combine(Folder, "MyGameLauncher.log");

    private static readonly object Lock = new();
    private const long MaxBytes = 1024 * 1024;

    public static void Info(string message) => Write("INFO ", message);

    public static void Error(string context, Exception ex) => Write("ERRO ", $"{context}{Environment.NewLine}{ex}");

    private static void Write(string level, string message)
    {
        lock (Lock)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes)
                    File.Move(FilePath, Path.Combine(Folder, "MyGameLauncher.old.log"), overwrite: true);

                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level} {message}{Environment.NewLine}", Encoding.UTF8);
            }
            catch { /* o registo nunca deve fazer a app falhar */ }
        }
    }

    public static void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Folder) { UseShellExecute = true });
        }
        catch { }
    }
}
