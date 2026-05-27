using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Detects login flow from swagger security schemes and performs authentication
/// to obtain bearer tokens for API personas.
/// </summary>
public sealed class SessionProvider(
    HttpClient httpClient,
    ILogger<SessionProvider> logger) : ISessionProvider
{
    public async Task<string?> AuthenticateAsync(
        string targetBaseUrl,
        SwaggerSpec spec,
        PersonaCredentials credentials,
        CancellationToken cancellationToken)
    {
        var loginEndpoint = DetectLoginEndpoint(spec, targetBaseUrl);
        if (loginEndpoint is null)
        {
            logger.LogWarning("No login endpoint detected in swagger spec — trying Basic auth probe");
            return await TryBasicAuthAsync(targetBaseUrl, credentials, cancellationToken);
        }

        return await PostLoginAsync(loginEndpoint, credentials, cancellationToken);
    }

    private string? DetectLoginEndpoint(SwaggerSpec spec, string baseUrl)
    {
        // Look for common login/auth endpoints in the spec
        var loginPaths = spec.Endpoints
            .Where(e => e.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
                        && (e.Path.Contains("login", StringComparison.OrdinalIgnoreCase)
                            || e.Path.Contains("auth", StringComparison.OrdinalIgnoreCase)
                            || e.Path.Contains("token", StringComparison.OrdinalIgnoreCase)
                            || e.Path.Contains("session", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (loginPaths.Count == 0)
            return null;

        // Prefer /auth/login or /login over generic /token endpoints
        var best = loginPaths
            .OrderByDescending(e => e.Path.Contains("login", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .First();

        return $"{baseUrl.TrimEnd('/')}{best.Path}";
    }

    private async Task<string?> PostLoginAsync(
        string loginUrl, PersonaCredentials credentials, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(new
        {
            username = credentials.Username,
            password = credentials.Password
        });

        var content = new StringContent(body, Encoding.UTF8, "application/json");

        try
        {
            var response = await httpClient.PostAsync(loginUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Login POST to {Url} returned {Status}", loginUrl, response.StatusCode);
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return ExtractToken(responseBody);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Login POST to {Url} failed", loginUrl);
            return null;
        }
    }

    private async Task<string?> TryBasicAuthAsync(
        string baseUrl, PersonaCredentials credentials, CancellationToken cancellationToken)
    {
        // Try a simple GET to root with Basic auth to see if the server returns a token
        using var request = new HttpRequestMessage(HttpMethod.Get, baseUrl);
        var basicCreds = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{credentials.Username}:{credentials.Password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicCreds);

        try
        {
            var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                // Server accepts Basic auth; use Basic auth as the "token"
                return $"Basic {basicCreds}";
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Basic auth probe to {Url} failed", baseUrl);
        }

        return null;
    }

    private static string? ExtractToken(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // Common token field names
            foreach (var field in new[] { "token", "access_token", "accessToken", "jwt", "id_token" })
            {
                if (root.TryGetProperty(field, out var tokenEl)
                    && tokenEl.ValueKind == JsonValueKind.String)
                    return tokenEl.GetString();
            }

            // Nested: { data: { token: "..." } }
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in new[] { "token", "access_token", "accessToken" })
                {
                    if (data.TryGetProperty(field, out var tokenEl)
                        && tokenEl.ValueKind == JsonValueKind.String)
                        return tokenEl.GetString();
                }
            }
        }
        catch
        {
            // Not JSON — ignore
        }

        return null;
    }
}
