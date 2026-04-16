using System.Diagnostics;
using System.Net.Http.Headers;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Executes authenticated HTTP probe requests for active API security skills.
/// Handles 401 responses by requesting re-authentication via a callback.
/// </summary>
public sealed class HttpProbeRunner(
    HttpClient httpClient,
    ILogger<HttpProbeRunner> logger)
{
    /// <summary>
    /// Executes a probe request with the persona's bearer token.
    /// If a 401 is received, calls <paramref name="reAuthCallback"/> for a new token
    /// and retries once.
    /// </summary>
    public async Task<HttpProbeResult> ProbeAsync(
        string persona,
        string method,
        string url,
        string? bearerToken,
        string? requestBody,
        Func<string, Task<string?>>? reAuthCallback,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteProbeAsync(persona, method, url, bearerToken, requestBody, cancellationToken);

        if (result.StatusCode == 401 && reAuthCallback is not null)
        {
            logger.LogInformation("HTTP 401 for persona '{Persona}' — attempting re-auth", persona);

            var newToken = await reAuthCallback(persona);
            if (newToken is not null)
            {
                result = await ExecuteProbeAsync(persona, method, url, newToken, requestBody, cancellationToken);
                logger.LogInformation("Re-auth for '{Persona}' succeeded, retry returned {Status}", persona, result.StatusCode);
            }
            else
            {
                logger.LogWarning("Re-auth for persona '{Persona}' failed — returning original 401 result", persona);
            }
        }

        return result;
    }

    private async Task<HttpProbeResult> ExecuteProbeAsync(
        string persona, string method, string url, string? bearerToken,
        string? requestBody, CancellationToken cancellationToken)
    {
        var httpMethod = new HttpMethod(method.ToUpperInvariant());
        using var request = new HttpRequestMessage(httpMethod, url);

        if (!string.IsNullOrWhiteSpace(bearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        if (requestBody is not null && httpMethod != HttpMethod.Get)
            request.Content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

        var sw = Stopwatch.StartNew();

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            sw.Stop();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var headers = response.Headers
                .Concat(response.Content.Headers)
                .ToDictionary(
                    h => h.Key,
                    h => string.Join(", ", h.Value),
                    StringComparer.OrdinalIgnoreCase);

            return new HttpProbeResult(
                persona, method.ToUpperInvariant(), url,
                (int)response.StatusCode, headers, body, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(ex, "Probe failed: {Method} {Url} for persona '{Persona}'", method, url, persona);

            return new HttpProbeResult(
                persona, method.ToUpperInvariant(), url,
                0, new Dictionary<string, string>(),
                $"Probe error: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }
}
