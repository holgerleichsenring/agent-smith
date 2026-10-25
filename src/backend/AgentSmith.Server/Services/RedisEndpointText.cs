using System.Text.RegularExpressions;

namespace AgentSmith.Server.Services;

/// <summary>
/// 2026-09-28-2ac3: the only text a finding or probe result may show for a Redis
/// configuration: the endpoints it names, or "(value hidden)". The configured string is
/// never echoed, because both forms can carry the password: <c>host:port,password=…</c>
/// in a key and <c>redis://:secret@host</c> in the host part.
/// </summary>
internal static partial class RedisEndpointText
{
    internal const string Hidden = "(value hidden)";

    internal static string Of(string? configuration)
    {
        var endpoints = (configuration ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(Endpoint)
            .OfType<string>()
            .ToList();
        return endpoints.Count == 0 ? Hidden : string.Join(", ", endpoints);
    }

    private static string? Endpoint(string token)
    {
        if (token.Contains("://")) return UriEndpoint(token);
        var match = PlainEndpoint().Match(token);
        return match.Success ? match.Value : null;
    }

    private static string? UriEndpoint(string token) =>
        Uri.TryCreate(token, UriKind.Absolute, out var uri) && uri.Host.Length > 0
            ? uri.Port > 0 ? $"{uri.Host}:{uri.Port}" : uri.Host
            : null;

    [GeneratedRegex(@"^([A-Za-z0-9._-]+|\[[0-9A-Fa-f:.]+\])(:\d{1,5})?$")]
    private static partial Regex PlainEndpoint();
}
