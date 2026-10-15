using AgentSmith.Server.Security;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0503a: one mapped route as the guard test reads it — the verb, the pattern, and what
/// the route DECLARES a caller needs. The declaration is a permission list (required
/// together) or the literal "anonymous", so a route moving between permissions is a
/// visible diff in the golden rather than a count that still adds up.
/// </summary>
internal sealed record RouteFact(
    string Method, string Pattern, IReadOnlyList<string> Permissions, string? AnonymousReason,
    ProjectFrom? Scope = null)
{
    internal const string Anonymous = "anonymous";

    internal bool IsDeclared => AnonymousReason is not null || Permissions.Count > 0;

    internal string Declaration =>
        AnonymousReason is not null ? Anonymous : string.Join(",", Permissions);

    /// <summary>2026-10-15-4a4db: where the route's project comes from, or "-".</summary>
    internal string Row => $"{Method}\t{Pattern}\t{Declaration}\t{Scope?.Wire ?? "-"}";
}
