using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SalesDemos.QuickLinks;

public partial class ScreenConnectClient : IDisposable
{
    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _http;
    private string? _antiForgeryToken;

    public ScreenConnectClient()
    {
        var handler = new HttpClientHandler { CookieContainer = _cookies, UseCookies = true };
        _http = new HttpClient(handler) { BaseAddress = new Uri(AppConfig.BaseUrl) };
        _http.DefaultRequestHeaders.Add("User-Agent", "SalesDemos.QuickLinks/1.0");
    }

    public void LoadPersistedAuth(AppConfig config)
    {
        if (config.AuthCookie is null) return;

        _cookies.Add(new Uri(AppConfig.BaseUrl), new Cookie(".ASPXAUTH", config.AuthCookie));
        _antiForgeryToken = config.AntiForgeryToken;
    }

    public async Task<bool> ValidateSessionAsync()
    {
        try
        {
            var token = await FetchAntiForgeryTokenAsync();
            if (token is null) return false;
            _antiForgeryToken = token;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<LoginResult> LoginAsync(string username, string password, string? otpCode = null)
    {
        // Fetch the login page to get the pre-auth anti-forgery token
        var loginPage = await _http.GetStringAsync("/Login");
        var preAuthToken = ExtractAntiForgeryToken(loginPage);
        if (preAuthToken is null)
            return new LoginResult(false, "Could not extract anti-forgery token from login page.");

        var nonce = GenerateNonce();
        var body = JsonSerializer.Serialize(new object?[] { username, password, otpCode, otpCode is not null, nonce });

        var request = new HttpRequestMessage(HttpMethod.Post, "/Services/AuthenticationService.ashx/TryLogin")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Anti-Forgery-Token", preAuthToken);

        var response = await _http.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();

        if (responseText.Trim() == "11")
            return new LoginResult(false, null, NeedsOtp: true);

        if (responseText.Trim() != "1")
            return new LoginResult(false, $"Login failed (server returned: {responseText.Trim()}).");

        // Login succeeded — extract the auth cookie and fetch the post-auth anti-forgery token
        var authCookie = _cookies.GetCookies(new Uri(AppConfig.BaseUrl))
            .FirstOrDefault(c => c.Name == ".ASPXAUTH");

        _antiForgeryToken = await FetchAntiForgeryTokenAsync();

        return new LoginResult(true, AuthCookie: authCookie?.Value, CookieExpiry: authCookie?.Expires);
    }

    public async Task<string?> GetAccessTokenAsync(string sessionId)
    {
        if (_antiForgeryToken is null)
            throw new InvalidOperationException("Not authenticated. Run setup first.");

        var body = JsonSerializer.Serialize(new object[] { new[] { AppConfig.SessionGroup }, sessionId });
        var request = new HttpRequestMessage(HttpMethod.Post, "/Services/PageService.ashx/GetAccessToken")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Anti-Forgery-Token", _antiForgeryToken);

        var response = await _http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                return null;
            response.EnsureSuccessStatusCode();
        }

        var responseText = await response.Content.ReadAsStringAsync();
        // Response is a JSON string like "DkT+R3cl..."
        return JsonSerializer.Deserialize<string>(responseText);
    }

    public static string BuildRelayUrl(string sessionId, string accessToken)
    {
        var parts = new[]
        {
            Uri.EscapeDataString(sessionId),
            Uri.EscapeDataString(AppConfig.RsaPublicKey),
            Uri.EscapeDataString(accessToken),
            "", // participant name (empty = uses logged-in user)
            "Access",
            "", // session title
            "", // client launch attributes
            ""  // logon session id
        };

        return $"{AppConfig.UrlScheme}://{AppConfig.RelayHost}:{AppConfig.RelayPort}/{string.Join("/", parts)}";
    }

    private async Task<string?> FetchAntiForgeryTokenAsync()
    {
        var hostPage = await _http.GetStringAsync("/Host");
        return ExtractAntiForgeryToken(hostPage);
    }

    private static string? ExtractAntiForgeryToken(string html)
    {
        var match = AntiForgeryTokenRegex().Match(html);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string GenerateNonce()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        return string.Create(16, chars, static (span, chars) =>
        {
            var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            Span<byte> bytes = stackalloc byte[16];
            rng.GetBytes(bytes);
            for (int i = 0; i < span.Length; i++)
                span[i] = chars[bytes[i] % chars.Length];
        });
    }

    [GeneratedRegex(@"""antiForgeryToken""\s*:\s*""([^""]+)""")]
    private static partial Regex AntiForgeryTokenRegex();

    public void Dispose() => _http.Dispose();
}

public record LoginResult(bool Success, string? ErrorMessage = null, bool NeedsOtp = false,
    string? AuthCookie = null, DateTime? CookieExpiry = null);
