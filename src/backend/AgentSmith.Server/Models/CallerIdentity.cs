namespace AgentSmith.Server.Models;

/// <summary>
/// p0503d: what the server made of a caller's token, and it is also the answer
/// <c>GET /api/identity</c> gives. One shape, because a second one that had to agree with
/// this one would eventually disagree.
/// <para>
/// A caller with NO roles is the case this exists for. It is the first login of an
/// installation that has just configured an authority: nothing is mapped yet, so the
/// operator needs to read the values their directory actually sent — which claim was
/// looked in, and what was in it — before a mapping can be written at all.
/// </para>
/// </summary>
public sealed record CallerIdentity(
    bool Authenticated,
    string? Subject,
    string? Issuer,
    string RoleClaim,
    string GroupClaim,
    IReadOnlyList<string> RoleClaimValues,
    IReadOnlyList<string> GroupClaimValues,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Findings);
