using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Series;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Contracts;
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
/// <para>2026-11-24-a4e7: a run that merged or armed a pull request has the ticket's records read from the host before the
/// act answers, so the page's next read shows the merge instead of the same Merge again.</para>
/// </summary>
public sealed class SeriesMergeNextAct(
    SeriesRunDetailReader details, TicketProjects projects, IPreviousAttemptReader attempts, IMergeGreenRun merges,
    IPrRecordRefreshNow records)
{
    public async Task<TicketCloseResult> MergeAsync(string seriesId, string ticketId, string askedBy, CancellationToken ct)
    {
        if (await details.ReadAsync(seriesId, ct, withHolds: false) is not { } series || projects.Of(series.Project) is not { } project)
            return TicketCloseResult.Refuse(Status404NotFound, $"There is no series {seriesId}.");
        if (series.Tickets.FirstOrDefault(t => t.TicketId == ticketId) is not { } ticket)
            return TicketCloseResult.Refuse(Status404NotFound, $"{ticketId} is not a ticket of this series.");
        if (series.MergeOrder.FirstOrDefault(s => s.TicketId == ticketId) is not { } step)
            return TicketCloseResult.Refuse(Status409Conflict, $"{ticket.Key} has no open pull request to merge.");
        // 2026-11-24-f9c5c: a ticket of a series with a branch merges into it under the series policy, which never arms.
        var policy = series.IntegrationBranch is null ? project.Merge : project.Merge.IntoSeriesBranch();
        if ((NotNext(step) ?? CiHolds(ticket, policy, series.IntegrationBranch)) is { } why) return TicketCloseResult.Refuse(Status409Conflict, why);
        if (await attempts.LatestAsync(project.Name, ticketId, null, ct) is not { } attempt)
            return TicketCloseResult.Refuse(Status409Conflict, $"{ticket.Key} has no attempt whose pull requests could merge.");
        return await RunAsync(project, ticket, Request(ticket, attempt.RunId, askedBy) with { SeriesBranch = series.IntegrationBranch }, ct);
    }

    // The ticket's pull requests that wait for a merge — per repo the open record, as the page shows them.
    private static CiGreenMergeRequest Request(SeriesRunTicketView ticket, string attemptRunId, string askedBy) =>
        new(ticket.TicketId, attemptRunId, TicketBranchNamer.Compose(new TicketId(ticket.TicketId)).Value,
            [.. ticket.PullRequests.Where(SeriesRunPullRequestView.Waits).Select(p => new CiGreenPullRequest(p.Repo, p.Url))])
        { AskedBy = askedBy };

    private async Task<TicketCloseResult> RunAsync(
        ResolvedProject project, SeriesRunTicketView ticket, CiGreenMergeRequest request, CancellationToken ct)
    {
        var slot = new ActSlot<IReadOnlyList<PullRequestMergeLine>>();
        if (!await merges.RunAsync(project, request, ct, slot))
            return TicketCloseResult.Refuse(Status409Conflict, $"{ticket.Key} was not merged: another run holds this ticket.");
        if (slot.Value is not { } lines)
            return TicketCloseResult.Refuse(Status500InternalServerError, $"{ticket.Key} was not merged: the merge run ended before its merge step.");
        if (lines.Any(l => l.Outcome is PullRequestCompletionOutcome.Merged or PullRequestCompletionOutcome.Armed))
            await records.RefreshAsync(project, ticket.TicketId);
        return TicketCloseResult.Ok(new SeriesMergeAnswer(ticket.TicketId, [.. lines.Select(View)]));
    }

    /// <summary>2026-11-23-849e: the ready step's CI is red — the act refuses what the page does not offer. 2026-11-23-0219: a
    /// CI that runs or has not started merges when green — the merge-green run re-queues a build that has not started, approves
    /// and arms the host's auto-complete, which only <c>respect_branch_protection: true</c> does; without it the act refuses and
    /// names the key. Null when the merge may go ahead. 2026-11-24-f9c5c: a merge into a series branch never arms; it waits for
    /// green and says so.</summary>
    internal static string? CiHolds(SeriesRunTicketView ticket, MergePolicyConfig merge, string? seriesBranch = null)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(merge);
        // 2026-11-25-9992a: the ticket's one CI (its waiting pull requests together, PullRequestHeadCi), as the page shows it
        if (ticket.Ci == CiStates.Red) return $"{ticket.Key} is not merged: its CI is red.";
        if (merge.RespectBranchProtection || !CiStates.Waits(ticket.Ci)) return null;
        var why = ticket.Ci == CiStates.NotStarted ? "its required build has not started" : "its CI is still running";
        if (seriesBranch is not null) return $"{ticket.Key} is not merged yet: {why}; a merge into {seriesBranch} waits for its CI to be green.";
        return $"{ticket.Key} is not merged yet: {why}, and merging once it is green arms the host's auto-complete, which needs "
            + $"merge.{BypassRefusal.Key}: true (Config → Project → Merge).";
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
