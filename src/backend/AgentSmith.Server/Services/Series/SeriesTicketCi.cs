using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-25-9992e: the series master's CI act on a member ticket: read has the ticket's records read from the host now
/// (<see cref="IPrRecordRefreshNow"/>) and answers each open pull request's CI on its head (<see cref="PullRequestHeadCi"/>);
/// requeue and start are a merge-green run over its open pull requests carrying NotStarted or NoCi — the paths the CI loop
/// took before — which merge nothing. 2026-11-26-5201f: never for a pull request on another branch than the series' or the default.
/// </summary>
public sealed class SeriesTicketCi(
    TicketProjects projects, PrReviewRecordRepository records, IPrRecordRefreshNow refresh, IPreviousAttemptReader attempts, IMergeGreenRun merges,
    SeriesRunPullRequests pullRequests)
{
    public async Task<string> ActAsync(SeriesRunRecord series, string ticketId, string act, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (projects.Of(series.Project) is not { } project) return $"Project '{series.Project}' is not configured.";
        if (act == SeriesCiActs.Read) await refresh.RefreshAsync(project, ticketId);
        var open = (await records.ByTicketAsync(project.Name, ticketId, ct)).Where(r => r.State == PrReviewStates.Open).ToList();
        if (open.Count == 0) return $"Ticket {ticketId} has no open pull request.";
        return act switch
        {
            SeriesCiActs.Read => string.Join(" ", open.Select(Line)),
            SeriesCiActs.Requeue or SeriesCiActs.Start => await RunAsync(project, series, ticketId, open, act, ct),
            _ => SeriesCiActs.Unknown(act),
        };
    }

    private async Task<string> RunAsync(ResolvedProject project, SeriesRunRecord series, string ticketId,
        IReadOnlyList<PrReviewRecordView> open, string act, CancellationToken ct)
    {
        if (Elsewhere(project, series, open) is { } stray) return stray; // 2026-11-26-5201f
        if (await attempts.LatestAsync(project.Name, ticketId, null, ct) is not { } attempt)
            return $"Ticket {ticketId} has no attempt whose pull requests the CI could be asked for.";
        var start = act == SeriesCiActs.Start;
        var request = new CiGreenMergeRequest(ticketId, attempt.RunId, TicketBranchNamer.Compose(new TicketId(ticketId)).Value,
            [.. open.Select(r => new CiGreenPullRequest(r.Repo, r.Url) { NoCi = start, NotStarted = !start })])
        { AskedBy = SeriesTicketMessage.AuthorOf(project), SeriesBranch = series.IntegrationBranch };
        if (!await merges.RunAsync(project, request, ct)) return $"Ticket {ticketId}: another run holds it, so its CI was not asked.";
        return $"Ticket {ticketId}: " + (start ? "its pull-request CI was asked to start" : "its required builds that had not started were queued again")
            + "; read it with ci read.";
    }

    // 2026-11-26-5201f: a pull request on another branch than the series' or the default branch has no build policy to run and
    // its CI is not read there, so a start or a requeue would "succeed" with nothing running — said instead, with the way on.
    private string? Elsewhere(ResolvedProject project, SeriesRunRecord series, IReadOnlyList<PrReviewRecordView> open)
    {
        var stray = pullRequests.Of(open, project).Where(p => pullRequests.TargetsElsewhere(p, project, series.IntegrationBranch)).ToList();
        return stray.Count == 0 ? null : string.Join(" ", stray.Select(p => $"{p.Repo} {p.Url} targets {p.Target}, neither the series nor the "
            + "default branch: no build policy validates it there and its CI is not read, so nothing would run. Retarget it with retarget_pull_request first."));
    }

    private static string Line(PrReviewRecordView record)
    {
        var ci = PullRequestHeadCi.OnHead(record);
        var failure = PullRequestHeadCi.FirstFailure(ci) is { } f ? $" — {f}" : string.Empty;
        return $"{record.Repo} {record.Url} into {record.TargetBranch ?? "an unread target"}: CI {ci?.State ?? "not read yet"}{failure}"
            + (record.HasConflicts ? ", conflicts with its base" : string.Empty) + (record.IsDraft ? ", draft" : string.Empty)
            + (record.ApprovalRequired ? ", waits for a person's approval on the host" : string.Empty) + "."; // 2026-11-26-5201f
    }
}
