namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-3c12: whether a cited finding is about ONE member of an entry group or about
/// the whole group at once.
/// <para>
/// "None of these entry points checks who is asking" has no start line. Under a
/// line-anchored rule it is unresolvable, and under a path-match rule it passes the moment
/// any file was read — which would make the strongest claim in a report the cheapest one to
/// fabricate. A <see cref="GroupWide"/> claim therefore cites the members it generalises
/// over and is settled against every one of them.
/// </para>
/// </summary>
public enum RequirementScope
{
    /// <summary>One member of the group, cited by file and line.</summary>
    Member,

    /// <summary>The whole group, cited by the members it generalises over.</summary>
    GroupWide
}
