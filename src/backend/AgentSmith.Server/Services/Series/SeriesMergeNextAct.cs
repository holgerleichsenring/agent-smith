using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.Tickets;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-23-31dce: the one merge of a series ticket — a person's from the series page, and (2026-11-25-9992e) the series
/// master's merge_pull_request. The ticket's pull requests that wait for a merge (per repo the open record, else one not read
/// yet) go to a merge-green run marked as asked by whoever asked, which runs under merge: manual too. The run re-reads every
/// base on the host and hands back each repo's outcome; a lease another run holds is said as such.
/// <para>2026-11-24-a4e7: a run that merged or armed a pull request has the ticket's records read from the host before the
/// act answers, so the page's next read shows the merge instead of the same Merge again.</para>
/// <para>2026-11-25-9992e: the merge is judged — whoever asked decided it — so no merge-order, CI, prerequisite, rung or base
/// refusal holds it; the host's own rules still answer. The master names the protection: now past it, or armed. It reads the
/// series and the records only, so a process without the dashboard's readers runs it as well.</para>
/// </summary>
public sealed class SeriesMergeNextAct(
    ISeriesRunStore series, TicketProjects projects, PrReviewRecordRepository pullRequests, IPreviousAttemptReader attempts,
    IMergeGreenRun merges, IPrRecordRefreshNow records)
{
    public async Task<TicketCloseResult> MergeAsync(string seriesId, string ticketId, string askedBy, CancellationToken ct,
        BranchProtection? protection = null)
    {
        if (await series.GetAsync(seriesId, ct) is not { } record || projects.Of(record.Project) is not { } project)
            return TicketCloseResult.Refuse(Status404NotFound, $"There is no series {seriesId}.");
        if (record.Tickets.FirstOrDefault(t => t.TicketId == ticketId) is not { } ticket)
            return TicketCloseResult.Refuse(Status404NotFound, $"{ticketId} is not a ticket of this series.");
        var waiting = await WaitingAsync(project.Name, ticketId, ct);
        if (waiting.Count == 0) return TicketCloseResult.Refuse(Status409Conflict, $"{ticket.Key} has no open pull request to merge.");
        if (await attempts.LatestAsync(project.Name, ticketId, null, ct) is not { } attempt)
            return TicketCloseResult.Refuse(Status409Conflict, $"{ticket.Key} has no attempt whose pull requests could merge.");
        var request = new CiGreenMergeRequest(ticketId, attempt.RunId, TicketBranchNamer.Compose(new TicketId(ticketId)).Value, waiting)
        {
            AskedBy = askedBy, Judged = true, SeriesBranch = record.IntegrationBranch, Protection = protection,
        };
        return await RunAsync(project, ticket, request, ct);
    }

    // Per repo the pull request that waits for a merge: the open record, else one the host has not been read for yet.
    private async Task<IReadOnlyList<CiGreenPullRequest>> WaitingAsync(string project, string ticketId, CancellationToken ct) =>
        [.. (await pullRequests.ByTicketAsync(project, ticketId, ct))
            .Where(r => r.State is PrReviewStates.Open or PrReviewStates.Unread)
            .GroupBy(r => r.Repo, StringComparer.Ordinal).Select(g => g.OrderBy(r => r.State == PrReviewStates.Open ? 0 : 1).First())
            .Select(r => new CiGreenPullRequest(r.Repo, r.Url))];

    private async Task<TicketCloseResult> RunAsync(ResolvedProject project, SeriesTicket ticket, CiGreenMergeRequest request, CancellationToken ct)
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

    private static SeriesMergeOutcomeView View(PullRequestMergeLine line) =>
        new(line.Repo, line.Outcome.ToString().ToLowerInvariant(), line.Text);
}
