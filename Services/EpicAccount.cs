using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameLauncher.Services;

/// <summary>
/// Ligação opcional à conta Epic Games, usada APENAS para ler o tempo de jogo dos jogos da Epic.
///
/// - O login é feito no site da Epic, no browser: a app nunca vê a palavra-passe.
/// - A app só faz dois pedidos: obter/renovar a autorização e ler o tempo de jogo.
///   Não compra, não descarrega, não altera nada na conta.
/// - A autorização fica guardada encriptada (Windows DPAPI) só neste PC e só para este utilizador do Windows.
/// - É a mesma ligação não oficial que o Legendary/Heroic usam; pode deixar de funcionar se a Epic a mudar.
/// </summary>
public sealed class EpicAccount
{
    // Identificação pública do launcher da Epic (a mesma usada pelo Legendary/Heroic).
    private const string ClientId = "34a02cf8f4414e29b15921876da36f9a";
    private const string ClientSecret = "daafbccc737745039dffe53d94fc76cf";
    private const string TokenUrl = "https://account-public-service-prod03.ol.epicgames.com/account/api/oauth/token";
    private const string KillUrl = "https://account-public-service-prod03.ol.epicgames.com/account/api/oauth/sessions/kill/";
    private const string PlaytimeUrl = "https://library-service.live.use1a.on.epicgames.com/library/api/public/playtime/account/{0}/all";

    /// <summary>Página de login da Epic; depois do login mostra um texto com o "authorizationCode".</summary>
    public const string LoginUrl =
        "https://www.epicgames.com/id/login?redirectUrl=https%3A%2F%2Fwww.epicgames.com%2Fid%2Fapi%2Fredirect%3FclientId%3D"
        + ClientId + "%26responseType%3Dcode";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static string SessionFile => Path.Combine(DataStore.Root, "epic-sessao.bin");

    public sealed class Session
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public string AccountId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
        public DateTime RefreshExpiresAt { get; set; }
        public DateTime? LastSync { get; set; }
    }

    public Session? Current { get; private set; }
    public bool IsConnected => Current is not null;

    public EpicAccount() => Current = LoadSession();

    /// <summary>Encontra o código no texto colado (o JSON todo da página, ou só o código).</summary>
    public static string? ExtractCode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = Regex.Match(text, "\"authorizationCode\"\\s*:\\s*\"([0-9a-fA-F]{32})\"");
        if (m.Success) return m.Groups[1].Value;
        m = Regex.Match(text.Trim(), "^\"?([0-9a-fA-F]{32})\"?$");
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Troca o código da página de login por uma autorização e guarda-a.</summary>
    public async Task<(bool Ok, string? Error)> ConnectAsync(string code)
    {
        var (session, error) = await RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["token_type"] = "eg1",
        });
        if (session is null) return (false, error);
        Current = session;
        SaveSession(session);
        return (true, null);
    }

    /// <summary>Apaga a autorização deste PC e termina a sessão na Epic.</summary>
    public async Task DisconnectAsync()
    {
        var s = Current;
        Current = null;
        try { File.Delete(SessionFile); } catch { }
        if (s is null) return;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, KillUrl + s.AccessToken);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.AccessToken);
            await Http.SendAsync(req);
        }
        catch { }
    }

    /// <summary>Tempo de jogo por jogo (chave = AppName da Epic), em minutos. null se não estiver ligado ou falhar.</summary>
    public async Task<Dictionary<string, int>?> GetPlaytimeAsync()
    {
        var s = await GetValidSessionAsync();
        if (s is null) return null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, string.Format(PlaytimeUrl, s.AccountId));
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.AccessToken);
            using var res = await Http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return null;

            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (!item.TryGetProperty("artifactId", out var idEl) || idEl.GetString() is not { } appName) continue;
                    long seconds = item.TryGetProperty("totalTime", out var t) && t.TryGetInt64(out var v) ? v : 0;
                    result[appName] = (int)(seconds / 60);
                }

            s.LastSync = DateTime.Now;
            SaveSession(s);
            return result;
        }
        catch { return null; }
    }

    // ------------------------------------------------------------ autorização

    private async Task<Session?> GetValidSessionAsync()
    {
        var s = Current;
        if (s is null) return null;
        if (s.ExpiresAt > DateTime.UtcNow.AddMinutes(5)) return s;

        if (s.RefreshExpiresAt <= DateTime.UtcNow) { Current = null; try { File.Delete(SessionFile); } catch { } return null; }

        var (renewed, _) = await RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = s.RefreshToken,
            ["token_type"] = "eg1",
        });
        if (renewed is null) return null;
        renewed.LastSync = s.LastSync;
        Current = renewed;
        SaveSession(renewed);
        return renewed;
    }

    private static async Task<(Session?, string?)> RequestTokenAsync(Dictionary<string, string> form)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, TokenUrl) { Content = new FormUrlEncodedContent(form) };
            req.Headers.Authorization = new AuthenticationHeaderValue("basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(ClientId + ":" + ClientSecret)));
            using var res = await Http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var j = doc.RootElement;

            if (!res.IsSuccessStatusCode)
                return (null, j.TryGetProperty("errorMessage", out var em) ? em.GetString() : res.ReasonPhrase);

            string Str(string n) => j.TryGetProperty(n, out var v) ? v.GetString() ?? "" : "";
            DateTime Date(string n) => DateTime.TryParse(Str(n), null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var d)
                ? d : DateTime.UtcNow.AddHours(1);

            return (new Session
            {
                AccessToken = Str("access_token"),
                RefreshToken = Str("refresh_token"),
                AccountId = Str("account_id"),
                DisplayName = Str("displayName"),
                ExpiresAt = Date("expires_at"),
                RefreshExpiresAt = Date("refresh_expires_at"),
            }, null);
        }
        catch (Exception ex) { return (null, ex.Message); }
    }

    // ------------------------------------------------------------ guardar (encriptado)

    private static Session? LoadSession()
    {
        try
        {
            if (!File.Exists(SessionFile)) return null;
            var bytes = ProtectedData.Unprotect(File.ReadAllBytes(SessionFile), null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<Session>(bytes);
        }
        catch { return null; }
    }

    private static void SaveSession(Session s)
    {
        try
        {
            Directory.CreateDirectory(DataStore.Root);
            var bytes = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(s), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(SessionFile, bytes);
        }
        catch { }
    }
}
