using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-08-25-4530: what this server expects of a caller, in the caller's hands. An
/// installation whose server enforces and whose dashboard was configured with no authority
/// answers 401 to everything and renders nothing, and the two settings that explain it sit
/// on different machines — the server does not know what the dashboard was given, and the
/// dashboard does not know what the server demands. This is the server's half; the
/// dashboard is the only place that holds both and can name the missing one.
/// <para>
/// Nothing here is secret. The authority is published in the discovery document any
/// browser can read, and enforcement announces itself with the first 401. The role
/// mapping, the group mapping and the admin grant answer a different question and are not
/// a caller's business.
/// </para>
/// </summary>
public sealed record AuthRequirements(bool Enforced, string? Authority, string? Audience)
{
    /// <summary>
    /// <see cref="TokenAuthorityConfig.Enforce"/> alone is not the answer.
    /// ServerAuthenticationExtensions attaches the fallback policy that refuses anything
    /// only once the authority is usable, so the switch on and no authority configured
    /// refuses nothing at all — and a caller told "enforced" there would go hunting a
    /// sign-in no route is asking for.
    /// </summary>
    public static AuthRequirements From(TokenAuthorityConfig auth) => new(
        auth is { IsUsable: true, Enforce: true },
        Configured(auth.Authority),
        Configured(auth.Audience));

    // Absent and blank are the same state to every reader of this, and only one of them
    // survives a round trip through a YAML key written with nothing after the colon.
    private static string? Configured(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
