using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// 2026-11-17-9a39a: which unlanded prerequisites a dependent may stack on. A prerequisite is GREEN when its newest
/// code attempt finished Success — every spec verified — without stopping at a series boundary, opened a pull request
/// itself, and the ticket carries no closed mark. The read is the run row the attempt already is; no second record.
/// <para>The STACK TIP is the one unlanded prerequisite whose requirement chain reaches every other unlanded one: its
/// branch already carries theirs. Two independent unlanded prerequisites have no tip — merging foreign branches into a
/// cut is refused, and the dependent waits.</para>
/// <para>2026-11-22-9719d: the reads go through <see cref="GreenReads"/>, which reads each prerequisite's end, close mark
/// and edges once for many dependents; whether a dependent started is one existence read for all of them.</para>
/// </summary>
public sealed class GreenPrerequisites(
    ITicketRequirementStore requirements, IPreviousAttemptReader attempts, ITicketClosedMarkStore closed)
{
    /// <summary>Whether this attempt pushed a green branch with a pull request.</summary>
    public static bool IsGreen(PreviousAttempt? attempt) =>
        attempt is { Finished: true, Status: RunStatuses.Success, StoppedAtBoundary: false, OpenedPullRequest: true };

    /// <summary>The tickets among <paramref name="ticketIds"/> that already have a code attempt — a dependent that
    /// started once was stacked already.</summary>
    public Task<IReadOnlySet<string>> StartedAsync(string project, IReadOnlyCollection<string> ticketIds, CancellationToken ct) =>
        attempts.AttemptedAsync(project, ticketIds, ct);

    /// <summary>The edge whose prerequisite is the stack tip, or null; with <paramref name="requireGreen"/> every
    /// unlanded prerequisite must be green as well.</summary>
    public Task<TicketRequirementEdge?> TipAsync(
        string project, IReadOnlyList<TicketRequirementEdge> unmet, bool requireGreen, CancellationToken ct) =>
        Reads(project).TipAsync(unmet, requireGreen, ct);

    /// <summary>A read set for the dependents of one project, seeded with requirement edges already read.</summary>
    public GreenReads Reads(
        string project, IEnumerable<KeyValuePair<(string Tracker, string Key), IReadOnlyList<TicketRequirementEdge>>>? known = null) =>
        new(requirements, attempts, closed, project, known);
}
