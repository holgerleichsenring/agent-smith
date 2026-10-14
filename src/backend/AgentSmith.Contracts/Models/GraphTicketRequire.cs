namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-10-14-dbcej: a ticket of a list requires a sibling by its key, or a ticket already filed in
/// the project's tracker as <c>ticket:&lt;id&gt;</c>. The proposal shows a numeric id as <c>#41</c>,
/// so <c>ticket:#41</c> names the same ticket as <c>ticket:41</c>.
/// </summary>
public static class GraphTicketRequire
{
    public const string ExistingPrefix = "ticket:";

    /// <summary>The tracker id after the prefix with one leading '#' stripped — empty when nothing
    /// follows; null when the requirement names a sibling.</summary>
    public static string? ExistingId(string requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        if (!requirement.StartsWith(ExistingPrefix, StringComparison.Ordinal)) return null;
        var id = requirement[ExistingPrefix.Length..].Trim();
        return id.StartsWith('#') ? id[1..].Trim() : id;
    }
}
