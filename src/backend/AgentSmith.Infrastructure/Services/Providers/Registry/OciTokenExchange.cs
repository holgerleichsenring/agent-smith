using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Exceptions;

namespace AgentSmith.Infrastructure.Services.Providers.Registry;

/// <summary>
/// 2026-11-23-933de: trades the pull user and token (Basic) for a bearer token scoped 'repository:&lt;repo&gt;:pull' at a
/// realm <see cref="OciChallenge.EnsureOn"/> already admitted. Answers null when the realm refuses the credentials. The
/// Basic value and the bearer token are never logged and never part of a refusal.
/// </summary>
public sealed class OciTokenExchange(HttpClient http, string loginServer, string pullUser, string pullToken)
{
    public async Task<string?> RequestAsync(OciChallenge challenge, string repository, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Address(challenge, repository));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{pullUser}:{pullToken}")));
        using var response = await SendAsync(request, repository, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return TokenOf(await response.Content.ReadAsStringAsync(cancellationToken))
            ?? throw new ContainerRegistryException(
                $"registry {loginServer} issued no token for {repository} (status {(int)response.StatusCode})");
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, string repository, CancellationToken cancellationToken)
    {
        try
        {
            return await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ContainerRegistryException(
                $"registry {loginServer} could not be reached for a token for {repository} ({ex.HttpRequestError})");
        }
    }

    private static Uri Address(OciChallenge challenge, string repository)
    {
        var query = $"scope={Uri.EscapeDataString($"repository:{repository}:pull")}";
        if (!string.IsNullOrEmpty(challenge.Service)) query = $"service={Uri.EscapeDataString(challenge.Service)}&{query}";
        return new UriBuilder(challenge.Realm) { Query = query }.Uri;
    }

    private static string? TokenOf(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            foreach (var name in new[] { "access_token", "token" })
                if (document.RootElement.TryGetProperty(name, out var value) && value.GetString() is { Length: > 0 } token)
                    return token;
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
