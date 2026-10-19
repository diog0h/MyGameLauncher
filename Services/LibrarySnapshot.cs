using System.Text.Json;
using GameLauncher.Models;

namespace GameLauncher.Services;

/// <summary>
/// Cópia leve da última lista de jogos (com os caminhos das imagens), em biblioteca.json.
/// Ao abrir, a app mostra logo esta lista e só depois procura jogos novos em segundo plano,
/// por isso o Início aparece quase instantaneamente.
/// </summary>
public static class LibrarySnapshot
{
    private static string FilePath => Path.Combine(DataStore.Root, "biblioteca.json");

    private sealed class Entry
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Store { get; set; } = "";
        public string? InstallPath { get; set; }
        public long SizeBytes { get; set; }
        public bool FullyInstalled { get; set; } = true;
        public string LaunchUri { get; set; } = "";
        public string? LaunchArguments { get; set; }
        public string? StoreDetailsUri { get; set; }
        public string? SteamAppId { get; set; }
        public bool IsCustom { get; set; }
        public DateTime? SteamLastPlayed { get; set; }
        public int StorePlaytimeMinutes { get; set; }
        public string? CoverPath { get; set; }
        public string? HeroPath { get; set; }
        public string? LogoPath { get; set; }
        public string? HeaderPath { get; set; }
    }

    public static void Save(IEnumerable<Game> games)
    {
        try
        {
            var list = games.Select(g => new Entry
            {
                Id = g.Id, Name = g.Name, Store = g.Store, InstallPath = g.InstallPath, SizeBytes = g.SizeBytes,
                FullyInstalled = g.FullyInstalled, LaunchUri = g.LaunchUri, LaunchArguments = g.LaunchArguments,
                StoreDetailsUri = g.StoreDetailsUri, SteamAppId = g.SteamAppId, IsCustom = g.IsCustom,
                SteamLastPlayed = g.SteamLastPlayed, StorePlaytimeMinutes = g.StorePlaytimeMinutes,
                CoverPath = g.CoverPath, HeroPath = g.HeroPath, LogoPath = g.LogoPath, HeaderPath = g.HeaderPath,
            }).ToList();
            Directory.CreateDirectory(DataStore.Root);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(list));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch (Exception ex) { AppLog.Error("Não foi possível guardar biblioteca.json", ex); }
    }

    public static List<Game> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var list = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(FilePath)) ?? new();
            return list.Where(e => e.Id.Length > 0 && e.LaunchUri.Length > 0).Select(e => new Game
            {
                Id = e.Id, Name = e.Name, Store = e.Store, InstallPath = e.InstallPath, SizeBytes = e.SizeBytes,
                FullyInstalled = e.FullyInstalled, LaunchUri = e.LaunchUri, LaunchArguments = e.LaunchArguments,
                StoreDetailsUri = e.StoreDetailsUri, SteamAppId = e.SteamAppId, IsCustom = e.IsCustom,
                SteamLastPlayed = e.SteamLastPlayed, StorePlaytimeMinutes = e.StorePlaytimeMinutes,
                // Só usa imagens que ainda existem.
                CoverPath = Existing(e.CoverPath), HeroPath = Existing(e.HeroPath),
                LogoPath = Existing(e.LogoPath), HeaderPath = Existing(e.HeaderPath),
            }).ToList();
        }
        catch (Exception ex)
        {
            AppLog.Error("biblioteca.json ilegível; a lista vai ser procurada do zero", ex);
            return new();
        }
    }

    private static string? Existing(string? path) => path is not null && File.Exists(path) ? path : null;
}
