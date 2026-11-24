using System.Text.RegularExpressions;
using AgentSmith.Contracts.Exceptions;

namespace AgentSmith.Infrastructure.Services.Providers.Registry;

/// <summary>
/// 2026-11-23-933de: a registry's 401 Bearer challenge — where to ask for a token (realm) and for which service. The
/// realm is checked against the login server BEFORE any credential is sent: https, and the login server's own host,
/// or the pull credentials would go wherever a registry or a party in the middle points them.
/// </summary>
public sealed partial record OciChallenge(Uri Realm, string? Service)
{
    /// <summary>The Bearer challenge of a response, or null when it carries none.</summary>
    public static OciChallenge? Parse(HttpResponseMessage response)
    {
        var bearer = response.Headers.WwwAuthenticate
            .FirstOrDefault(h => string.Equals(h.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase));
        if (bearer?.Parameter is not { } parameter) return null;
        var values = Pair().Matches(parameter)
            .ToDictionary(m => m.Groups["key"].Value.ToLowerInvariant(), m => m.Groups["value"].Value);
        return values.TryGetValue("realm", out var realm) && Uri.TryCreate(realm, UriKind.Absolute, out var uri)
            ? new OciChallenge(uri, values.GetValueOrDefault("service"))
            : null;
    }

    /// <summary>Refuses a realm that is not https on <paramref name="loginServer"/>, naming both hosts.</summary>
    public void EnsureOn(string loginServer)
    {
        if (Realm.Scheme == Uri.UriSchemeHttps && string.Equals(Realm.Host, loginServer, StringComparison.OrdinalIgnoreCase))
            return;
        throw new ContainerRegistryException($"registry {loginServer} names the token realm {Realm.Scheme}://{Realm.Host}; "
            + $"the pull token is only sent over https to {loginServer} itself, so nothing was sent");
    }

    [GeneratedRegex("(?<key>[A-Za-z]+)=\"(?<value>[^\"]*)\"")]
    private static partial Regex Pair();
}
