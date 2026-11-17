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
/// </summary>
public sealed class GreenPrerequisites(
    ITicketRequirementStore requirements, IPreviousAttemptReader attempts, ITicketClosedMarkStore closed)
{
    /// <summary>Whether this attempt pushed a green branch with a pull request.</summary>
    public static bool IsGreen(PreviousAttempt? attempt) =>
        attempt is { Finished: true, Status: RunStatuses.Success, StoppedAtBoundary: false, OpenedPullRequest: true };

    /// <summary>Whether the ticket already has a code attempt — a dependent that started once was stacked already.</summary>
    public async Task<bool> StartedAsync(string project, string ticketId, CancellationToken ct) =>
        await attempts.LatestAsync(project, ticketId, null, ct) is not null;

    /// <summary>The edge whose prerequisite is the stack tip, or null; with <paramref name="requireGreen"/> every
    /// unlanded prerequisite must be green as well.</summary>
    public async Task<TicketRequirementEdge?> TipAsync(
        string project, IReadOnlyList<TicketRequirementEdge> unmet, bool requireGreen, CancellationToken ct)
    {
        var distinct = unmet.DistinctBy(e => e.RequiredTicketKey, StringComparer.Ordinal).ToList();
        if (distinct.Count == 0) return null;
        if (requireGreen && !await AllGreenAsync(project, distinct, ct)) return null;
        foreach (var candidate in distinct)
            if (await ReachesAllAsync(candidate, distinct, ct)) return candidate;
        return null;
    }

    private async Task<bool> AllGreenAsync(string project, IReadOnlyList<TicketRequirementEdge> unmet, CancellationToken ct)
    {
        if ((await closed.OfAsync(unmet[0].Tracker, [.. unmet.Select(e => e.RequiredTicketKey)], ct)).Count > 0) return false;
        foreach (var edge in unmet)
            if (!IsGreen(await attempts.LatestAsync(project, edge.RequiredTicketId, null, ct))) return false;
        return true;
    }

    // Walks the candidate's requirement chain; the seen set bounds it on any graph.
    private async Task<bool> ReachesAllAsync(
        TicketRequirementEdge candidate, IReadOnlyList<TicketRequirementEdge> unmet, CancellationToken ct)
    {
        var others = unmet.Select(e => e.RequiredTicketKey).Where(k => k != candidate.RequiredTicketKey)
            .ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal) { candidate.RequiredTicketKey };
        var frontier = new Queue<string>([candidate.RequiredTicketKey]);
        while (others.Count > 0 && frontier.TryDequeue(out var key))
            foreach (var edge in await requirements.RequiredOfAsync(candidate.Tracker, key, ct))
            {
                if (!seen.Add(edge.RequiredTicketKey)) continue;
                others.Remove(edge.RequiredTicketKey);
                frontier.Enqueue(edge.RequiredTicketKey);
            }
        return others.Count == 0;
    }
}
