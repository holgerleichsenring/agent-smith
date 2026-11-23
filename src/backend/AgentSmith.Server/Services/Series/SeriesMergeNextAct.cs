using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Series;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.Tickets;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-23-31dce: a person merges the next ticket of a series from its page. The merge order is read again, so a ticket
/// that is not next is refused with what comes first and nothing starts; the ready one's open pull requests (per repo
/// the open record, as the page shows them) go to a merge-green run marked as asked by that person, which runs under
/// merge: manual too. The run re-reads every base on the host and hands back each repo's outcome; a lease another run
/// holds is said as such. The retarget of the next ticket follows the landing, as for any merge.
/// </summary>
public sealed class SeriesMergeNextAct(
    SeriesRunDetailReader details, TicketProjects projects, IPreviousAttemptReader attempts, IMergeGreenRun merges)
{
    public async Task<TicketCloseResult> MergeAsync(string seriesId, string ticketId, string askedBy, CancellationToken ct)
    {
        if (await details.ReadAsync(seriesId, ct, withHolds: false) is not { } series || projects.Of(series.Project) is not { } project)
            return TicketCloseResult.Refuse(Status404NotFound, $"There is no series {seriesId}.");
        if (series.Tickets.FirstOrDefault(t => t.TicketId == ticketId) is not { } ticket)
            return TicketCloseResult.Refuse(Status404NotFound, $"{ticketId} is not a ticket of this series.");
        if (series.MergeOrder.FirstOrDefault(s => s.TicketId == ticketId) is not { } step)
            return TicketCloseResult.Refuse(Status409Conflict, $"{ticket.Key} has no open pull request to merge.");
        if (NotNext(step) is { } why) return TicketCloseResult.Refuse(Status409Conflict, why);
        if (await attempts.LatestAsync(project.Name, ticketId, null, ct) is not { } attempt)
            return TicketCloseResult.Refuse(Status409Conflict, $"{ticket.Key} has no attempt whose pull requests could merge.");
        return await RunAsync(project, ticket, Request(ticket, attempt.RunId, askedBy), ct);
    }

    // The ticket's pull requests that wait for a merge — per repo the open record, as the page shows them.
    private static CiGreenMergeRequest Request(SeriesRunTicketView ticket, string attemptRunId, string askedBy) =>
        new(ticket.TicketId, attemptRunId, TicketBranchNamer.Compose(new TicketId(ticket.TicketId)).Value,
            [.. ticket.PullRequests.Where(SeriesRunPullRequests.Waits).Select(p => new CiGreenPullRequest(p.Repo, p.Url))])
        { AskedBy = askedBy };

    private async Task<TicketCloseResult> RunAsync(
        ResolvedProject project, SeriesRunTicketView ticket, CiGreenMergeRequest request, CancellationToken ct)
    {
        var slot = new ActSlot<IReadOnlyList<PullRequestMergeLine>>();
        if (!await merges.RunAsync(project, request, ct, slot))
            return TicketCloseResult.Refuse(Status409Conflict, $"{ticket.Key} was not merged: another run holds this ticket.");
        return slot.Value is { } lines
            ? TicketCloseResult.Ok(new SeriesMergeAnswer(ticket.TicketId, [.. lines.Select(View)]))
            : TicketCloseResult.Refuse(Status500InternalServerError, $"{ticket.Key} was not merged: the merge run ended before its merge step.");
    }

    /// <summary>Why a step of the merge order is not the one to merge now; null when it is.</summary>
    internal static string? NotNext(SeriesRunMergeStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return step.Wait switch
        {
            SeriesRunMergeWaits.Ready => null,
            SeriesRunMergeWaits.Retarget => $"{step.Key} is not next: a pull request of it still targets another branch and is "
                + "moved onto the default branch first.",
            _ => $"{step.Key} is not next: {string.Join(" and ", step.After)} {(step.After.Count == 1 ? "merges" : "merge")} first.",
        };
    }

    private static SeriesMergeOutcomeView View(PullRequestMergeLine line) =>
        new(line.Repo, line.Outcome.ToString().ToLowerInvariant(), line.Text);
}
