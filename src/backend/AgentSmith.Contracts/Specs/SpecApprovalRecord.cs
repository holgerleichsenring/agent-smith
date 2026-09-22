namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-09-17-0e79a: what an approval in the design conversation left behind for the run that
/// works the ticket it filed — the set, the approval it carries, and the repositories the
/// approval named.
/// <para>
/// The three travel together under ONE key. ScopeRepos needs the repositories before any
/// sandbox exists and DeriveSpec needs the set and its approval ten steps later; splitting
/// them would let a run scope by an approval it then does not use.
/// </para>
/// <para>
/// The record is never CONSUMED. It is what a later approval is compared against and what the
/// conversation showed the operator, so a re-trigger before the first publish is handed the
/// same record again.
/// </para>
/// </summary>
/// <param name="Key">The spec-set key of the ticket the approval filed — <c>&lt;provider&gt;-&lt;ticketId&gt;</c>.</param>
/// <param name="Set">The approved set, carrying its own <see cref="SpecSet.Approval"/>.</param>
/// <param name="Repositories">The repositories the approval named, by configured name.</param>
/// <param name="Tracker">
/// The tracker CONNECTION the ticket lives on, by its catalog name. The spec key carries the
/// tracker TYPE and the ticket id, so two Jira instances — or two Azure DevOps organizations —
/// numbering a ticket alike would otherwise resolve to one record. The key stays as it is
/// because it is also the git path and the pointer's id; the instance is compared beside it.
/// </param>
/// <param name="CarryingRepo">
/// 2026-09-22-b6ad: which of <paramref name="Repositories"/> carries the set — the repository
/// FILING wrote the branch into, by its configured name. Empty on a record written before this
/// phase, and on one whose approval named no configured repository, which reads as "nobody chose"
/// and leaves the run taking its own first-scoped repository exactly as it did.
/// <para>
/// It sits AFTER <paramref name="Tracker"/> because the sole construction site is positional and
/// both are strings: inserted earlier it would compile in silence and write the tracker name as
/// the carrier.
/// </para>
/// </param>
public sealed record SpecApprovalRecord(
    string Key,
    SpecSet Set,
    IReadOnlyList<string> Repositories,
    string Tracker = "",
    string CarryingRepo = "")
{
    /// <summary>The approval the set carries — null only on a record built without one.</summary>
    public SpecApproval? Approval => Set.Approval;
}
