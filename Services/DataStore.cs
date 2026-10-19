using System.Text.Json;
using GameLauncher.Models;

namespace GameLauncher.Services;

/// <summary>Lê e grava os dados da app em %LocalAppData%\GameLauncher.</summary>
public static class DataStore
{
    public static string Root { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameLauncher");

    public static string CacheDir => Path.Combine(Root, "cache");
    private static string DataFile => Path.Combine(Root, "dados.json");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly object Lock = new();

    public static UserData Load()
    {
        try
        {
            if (File.Exists(DataFile))
                return JsonSerializer.Deserialize<UserData>(File.ReadAllText(DataFile), Json) ?? new UserData();
        }
        catch
        {
            // Ficheiro estragado: guarda uma cópia e começa do zero, em vez de a app não abrir.
            try { File.Copy(DataFile, DataFile + ".estragado", overwrite: true); } catch { }
        }
        return new UserData();
    }

    public static void Save(UserData data)
    {
        lock (Lock)
        {
            try
            {
                Directory.CreateDirectory(Root);
                var tmp = DataFile + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(data, Json));
                File.Move(tmp, DataFile, overwrite: true);
            }
            catch { /* não deve impedir a app de funcionar */ }
        }
    }
}
