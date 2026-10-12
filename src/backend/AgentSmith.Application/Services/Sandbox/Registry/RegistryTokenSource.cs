using System.Text.Json;
using AgentSmith.Application.Models.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox.Registry;

/// <summary>
/// 2026-10-12-50bdb: fetches the anonymous pull token a registry's Bearer challenge points at
/// (Docker Hub: auth.docker.io; ghcr.io: ghcr.io/token). Null when no token was handed out —
/// the caller then keeps the registry's 401, which is an unverified image, not a missing one.
/// </summary>
public sealed class RegistryTokenSource(RegistryTransport transport, ILogger<RegistryTokenSource> logger)
{
    private const string Json = "application/json";

    public async Task<string?> TokenAsync(RegistryChallenge challenge, CancellationToken cancellationToken)
    {
        var reply = await transport.SendAsync(HttpMethod.Get, Address(challenge), Json, null, cancellationToken);
        return reply.Status == 200 ? Token(reply.Body) : null;
    }

    private static Uri Address(RegistryChallenge challenge)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(challenge.Service)) query.Add("service=" + Uri.EscapeDataString(challenge.Service));
        if (!string.IsNullOrEmpty(challenge.Scope)) query.Add("scope=" + Uri.EscapeDataString(challenge.Scope));
        return new UriBuilder(challenge.Realm) { Query = string.Join('&', query) }.Uri;
    }

    // Docker Hub and ghcr.io answer "token"; the distribution spec also allows "access_token".
    private string? Token(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            foreach (var name in new[] { "token", "access_token" })
                if (root.TryGetProperty(name, out var token) && token.ValueKind == JsonValueKind.String)
                    return token.GetString();
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "The registry token service answered something that is not JSON");
            return null;
        }
    }
}
