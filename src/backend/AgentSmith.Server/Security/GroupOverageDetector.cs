using System.Security.Claims;

namespace AgentSmith.Server.Security;

/// <summary>
/// p0503d: tells "this caller is in no mapped group" apart from "this token could not
/// carry its groups at all". Past roughly two hundred memberships Entra omits the group
/// claim and emits <c>_claim_names</c> and <c>_claim_sources</c> instead — JSON OBJECTS,
/// which arrive as a single claim whose value is JSON text — and a token delivered through
/// the URL fragment carries <c>hasgroups</c>. Mapping such a caller by group silently
/// grants nothing, which reads exactly like a mapping the operator got wrong.
/// <para>
/// Detection is by claim PRESENCE, never by a parsed value: the shapes differ per marker
/// and per delivery, the names do not.
/// </para>
/// </summary>
internal static class GroupOverageDetector
{
    private static readonly string[] Markers = ["_claim_names", "_claim_sources", "hasgroups"];

    public static IReadOnlyList<string> Findings(ClaimsPrincipal caller) =>
        [.. Markers.Where(marker => caller.HasClaim(claim => claim.Type == marker))
            .Select(marker =>
                $"The token carries '{marker}', which means the directory left its group "
                + "claim out because the caller is in too many groups. No group mapping can "
                + "resolve this caller; grant the role through a role claim instead.")];
}
