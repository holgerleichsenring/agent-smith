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
/// <para>
/// 2026-08-25-1806: it also carries whether THIS caller's token was refused, and by which
/// check. An enforcing installation answers /api/identity with 401 to exactly the caller
/// whose token it rejected, so this anonymous route is the only one that can say why — and
/// a refused token rendered as "nothing arrived" sends an operator to write a mapping when
/// the audience is what is wrong.
/// </para>
/// </summary>
public sealed record AuthRequirements(bool Enforced, string? Authority, string? Audience)
{
    /// <summary>
    /// Which check refused this request's token, out of the closed <c>TokenRefusals</c>
    /// vocabulary — null when no token was presented, or when the one presented was
    /// accepted. A classification rather than the validation message: it is enough to act
    /// on, and it repeats nothing back that the caller did not send.
    /// </summary>
    public string? TokenRefusal { get; init; }

    /// <summary>
    /// 2026-09-14-c72e: the audience the REFUSED token carried, null when none was refused or
    /// what arrived could not be decoded. Read from this request's own bearer, unverified, and
    /// published beside <see cref="Audience"/> so the difference is a comparison rather than a
    /// hunt — which is what the closed refusal vocabulary alone could not give: it names the
    /// check that failed, never the value that failed it.
    /// </summary>
    public string? PresentedAudience { get; init; }

    /// <summary>The issuer the refused token carried, on the same terms as <see cref="PresentedAudience"/>.</summary>
    public string? PresentedIssuer { get; init; }

    /// <summary>
    /// The refused token's own version claim, on the same terms. It is the field that turns two
    /// values that differ into a shape with a name — a directory that mints more than one token
    /// version for one sign-in is the case an operator cannot otherwise see.
    /// </summary>
    public string? PresentedTokenVersion { get; init; }

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
