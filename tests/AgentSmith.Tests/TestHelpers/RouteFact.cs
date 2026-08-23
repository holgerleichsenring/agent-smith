namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0503a: one mapped route as the guard test reads it — the verb, the pattern, and what
/// the route DECLARES a caller needs. The declaration is a permission list (required
/// together) or the literal "anonymous", so a route moving between permissions is a
/// visible diff in the golden rather than a count that still adds up.
/// </summary>
internal sealed record RouteFact(
    string Method, string Pattern, IReadOnlyList<string> Permissions, string? AnonymousReason)
{
    internal const string Anonymous = "anonymous";

    internal bool IsDeclared => AnonymousReason is not null || Permissions.Count > 0;

    internal string Declaration =>
        AnonymousReason is not null ? Anonymous : string.Join(",", Permissions);

    internal string Row => $"{Method}\t{Pattern}\t{Declaration}";
}
