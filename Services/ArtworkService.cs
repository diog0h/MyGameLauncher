using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using GameLauncher.Models;

namespace GameLauncher.Services;

public enum ArtKind { Cover, Hero, Logo, Header }

/// <summary>
/// Encontra as imagens de cada jogo e guarda-as no PC. Ordem de procura:
/// 1) cache da própria Steam (appcache\librarycache) — instantâneo e offline;
/// 2) cache desta app;
/// 3) CDN da Steam;
/// 4) SteamGridDB (se houver chave).
/// </summary>
public sealed class ArtworkService
{
    private static readonly HttpClient Http = CreateClient();
    private readonly SemaphoreSlim _gate = new(4);
    private readonly string? _steamRoot;
    private readonly string? _sgdbKey;

    /// <summary>Quanto tempo esperar antes de voltar a tentar uma imagem que não existia.</summary>
    private static readonly TimeSpan MissingRetry = TimeSpan.FromDays(7);

    public ArtworkService(string? steamRoot, string? steamGridDbKey)
    {
        _steamRoot = steamRoot;
        _sgdbKey = string.IsNullOrWhiteSpace(steamGridDbKey) ? null : steamGridDbKey.Trim();
        Directory.CreateDirectory(DataStore.CacheDir);
    }

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("MyGameLauncher/1.0");
        return c;
    }

    /// <summary>Imagem de um jogo de qualquer loja (Steam: imagens oficiais; outras: SteamGridDB pelo nome).</summary>
    public Task<string?> GetAsync(GameLauncher.Models.Game game, ArtKind kind, CancellationToken ct = default) =>
        game.SteamAppId is { } appId
            ? GetAsync(appId, kind, ct)
            : GetByNameAsync(game.Id, game.Name, kind, ct);

    private async Task<string?> GetByNameAsync(string id, string name, ArtKind kind, CancellationToken ct)
    {
        if (kind == ArtKind.Header) return null; // o SteamGridDB não tem este formato
        var cached = CachePath(id, kind);
        if (File.Exists(cached)) return cached;
        if (_sgdbKey is null) return null;
        var missing = cached + ".none";
        if (File.Exists(missing) && DateTime.UtcNow - File.GetLastWriteTimeUtc(missing) < MissingRetry) return null;

        await _gate.WaitAsync(ct);
        try
        {
            var sgdbId = await FindSteamGridDbGameIdAsync(name, ct);
            if (sgdbId is not null)
            {
                var url = await FindOnSteamGridDbAsync($"game/{sgdbId}", kind, ct);
                if (url is not null && await TryDownloadAsync(url, cached, ct)) return cached;
            }
            try { File.WriteAllText(missing, ""); } catch { }
            return null;
        }
        finally { _gate.Release(); }
    }

    private static string CachePath(string id, ArtKind kind)
    {
        var safe = string.Concat(id.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ':' ? '_' : c));
        var ext = kind == ArtKind.Logo ? ".png" : ".jpg";
        return Path.Combine(DataStore.CacheDir, $"{safe}_{kind.ToString().ToLowerInvariant()}{ext}");
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task<string?>> _sgdbIds = new();

    /// <summary>Procura o jogo no SteamGridDB pelo nome (uma vez por nome).</summary>
    private Task<string?> FindSteamGridDbGameIdAsync(string name, CancellationToken ct) =>
        _sgdbIds.GetOrAdd(name, async n =>
        {
            try
            {
                var clean = string.Concat(n.Where(c => c is not ('™' or '®' or '©'))).Trim();
                using var req = new HttpRequestMessage(HttpMethod.Get,
                    "https://www.steamgriddb.com/api/v2/search/autocomplete/" + Uri.EscapeDataString(clean));
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sgdbKey);
                using var res = await Http.SendAsync(req, ct);
                if (!res.IsSuccessStatusCode) return null;
                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
                if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                    foreach (var item in data.EnumerateArray())
                        if (item.TryGetProperty("id", out var idEl)) return idEl.ToString();
            }
            catch { }
            return null;
        });

    public async Task<string?> GetAsync(string appId, ArtKind kind, CancellationToken ct = default)
    {
        // 1) cache da Steam
        var local = FindInSteamCache(appId, kind);
        if (local is not null) return local;

        // 2) cache da app
        var cached = CachePath(appId, kind);
        if (File.Exists(cached)) return cached;
        var missing = cached + ".none";
        if (File.Exists(missing) && DateTime.UtcNow - File.GetLastWriteTimeUtc(missing) < MissingRetry) return null;

        await _gate.WaitAsync(ct);
        try
        {
            // 3) CDN da Steam
            foreach (var url in SteamCdnUrls(appId, kind))
                if (await TryDownloadAsync(url, cached, ct)) return cached;

            // 4) SteamGridDB
            if (_sgdbKey is not null && kind != ArtKind.Header)
            {
                var url = await FindOnSteamGridDbAsync($"steam/{appId}", kind, ct);
                if (url is not null && await TryDownloadAsync(url, cached, ct)) return cached;
            }

            try { File.WriteAllText(missing, ""); } catch { }
            return null;
        }
        finally { _gate.Release(); }
    }

    private string? FindInSteamCache(string appId, ArtKind kind)
    {
        if (_steamRoot is null) return null;
        var lib = Path.Combine(_steamRoot, "appcache", "librarycache");
        if (!Directory.Exists(lib)) return null;

        string[] names = kind switch
        {
            ArtKind.Cover => new[] { "library_600x900.jpg", "library_600x900_2x.jpg", "library_capsule.jpg" },
            ArtKind.Hero => new[] { "library_hero.jpg", "library_hero_2x.jpg" },
            ArtKind.Logo => new[] { "logo.png", "logo_2x.png" },
            _ => new[] { "header.jpg", "library_header.jpg" },
        };

        try
        {
            // Formato novo: librarycache\<appid>\(...)\library_600x900.jpg
            var dir = Path.Combine(lib, appId);
            if (Directory.Exists(dir))
            {
                foreach (var name in names)
                {
                    var hit = Directory.EnumerateFiles(dir, name, SearchOption.AllDirectories).FirstOrDefault();
                    if (hit is not null) return hit;
                }
            }
            // Formato antigo: librarycache\<appid>_library_600x900.jpg
            foreach (var name in names)
            {
                var old = Path.Combine(lib, $"{appId}_{name}");
                if (File.Exists(old)) return old;
            }
        }
        catch { }
        return null;
    }

    private static IEnumerable<string> SteamCdnUrls(string appId, ArtKind kind)
    {
        var file = kind switch
        {
            ArtKind.Cover => "library_600x900.jpg",
            ArtKind.Hero => "library_hero.jpg",
            ArtKind.Logo => "logo.png",
            _ => "header.jpg",
        };
        yield return $"https://shared.steamstatic.com/store_item_assets/steam/apps/{appId}/{file}";
        yield return $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/{file}";
    }

    /// <param name="target">"steam/&lt;appid&gt;" ou "game/&lt;id do SteamGridDB&gt;".</param>
    private async Task<string?> FindOnSteamGridDbAsync(string target, ArtKind kind, CancellationToken ct)
    {
        const string filters = "types=static&nsfw=false&humor=false";
        var endpoint = kind switch
        {
            ArtKind.Cover => $"grids/{target}?dimensions=600x900&{filters}",
            ArtKind.Hero => $"heroes/{target}?{filters}",
            _ => $"logos/{target}?{filters}",
        };
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://www.steamgriddb.com/api/v2/" + endpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sgdbKey);
            using var res = await Http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                foreach (var item in data.EnumerateArray())
                    if (item.TryGetProperty("url", out var u) && u.GetString() is { Length: > 0 } url)
                        return url;
        }
        catch { }
        return null;
    }

    private static async Task<bool> TryDownloadAsync(string url, string target, CancellationToken ct)
    {
        try
        {
            using var res = await Http.GetAsync(url, ct);
            if (!res.IsSuccessStatusCode) return false;
            var type = res.Content.Headers.ContentType?.MediaType ?? "";
            if (!type.StartsWith("image/")) return false;

            var bytes = await res.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length < 1024) return false;
            var tmp = target + ".tmp";
            await File.WriteAllBytesAsync(tmp, bytes, ct);
            File.Move(tmp, target, overwrite: true);
            return true;
        }
        catch { return false; }
    }

    public static void ClearCache()
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(DataStore.CacheDir)) { try { File.Delete(f); } catch { } }
        }
        catch { }
    }
}
