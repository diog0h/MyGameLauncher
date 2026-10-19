using System.Net.Http;
using System.Text.Json;

namespace GameLauncher.Services;

/// <summary>Vê no GitHub se há uma versão mais recente da app (só lê a página pública das versões).</summary>
public static class UpdateChecker
{
    public const string Repository = "diog0h/MyGameLauncher";
    public static string ReleasesUrl => $"https://github.com/{Repository}/releases";

    public sealed record Update(Version Version, string Tag, string Url);

    public static Version CurrentVersion
    {
        get
        {
            var v = typeof(UpdateChecker).Assembly.GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }
    }

    public static string CurrentVersionText => CurrentVersion.ToString(3);

    /// <summary>Devolve a versão nova, null se já está atualizada. Lança exceção se não conseguir ver.</summary>
    public static async Task<Update?> CheckAsync(CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"MyGameLauncher/{CurrentVersionText}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        using var res = await http.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest", ct);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return null; // ainda não há versões publicadas
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        var url = root.TryGetProperty("html_url", out var u) ? u.GetString() ?? ReleasesUrl : ReleasesUrl;
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest)) return null;

        latest = new Version(latest.Major, latest.Minor, Math.Max(0, latest.Build));
        return latest > CurrentVersion ? new Update(latest, tag, url) : null;
    }
}
