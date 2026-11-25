using AgentSmith.Application.Models;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using Providers = AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-023fa: a green code run merges its own pull requests when the project says merge: on_gate_green — the
/// operator's "merge when the run's own gate is green and fix forward". Only the pull requests CommitAndPR published
/// as gate-passed are merged, in order, in the project's protection mode (<see cref="PullRequestMergeSequence"/>);
/// nothing here fails the run, whose work is committed and open either way.
/// <para>2026-11-17-3435b: with merge: on_ci_green the code run merges nothing: each gate-passed pull request is recorded
/// awaiting_ci, and the server's CI watch starts a merge-green run (<see cref="ContextKeys.CiGreenMerge"/>) once CI is green.
/// 2026-11-23-31dce: a request somebody asked for (AskedBy) runs under merge: manual too; each repo's outcome fills the
/// outcome slot when the server seeded one.</para>
/// <para>2026-11-24-629aa/ab: a request for not-started builds queues them again (<see cref="PullRequestBuildRequeue"/>), one for
/// a dropped auto-complete arms it again (<see cref="PullRequestAutoCompleteRearm"/>); 2026-11-24-f9c5d: one for pull requests
/// without CI starts it (<see cref="PullRequestCiStart"/>) — 2026-11-25-9992e: only the series master asks, and none merges.</para>
/// <para>2026-11-25-9992e: a code run of a ticket an active series holds merges nothing and marks nothing awaiting CI — the
/// series master merges it; a judged request (<see cref="CiGreenMergeRequest.Judged"/>) passes the prerequisite, rung and base
/// refusals.</para>
/// </summary>
public sealed class CompletePullRequestsHandler(
    PullRequestMergeSequence merges, PullRequestCompleter completer, PullRequestCiStart? ciStart = null, PullRequestBuildRequeue? requeue = null,
    PullRequestAutoCompleteRearm? rearm = null, AgentSmith.Contracts.Series.ISeriesRunStore? series = null)
    : ICommandHandler<CompletePullRequestsContext>
{
    public async Task<CommandResult> ExecuteAsync(CompletePullRequestsContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Pipeline.TryGet<CiGreenMergeRequest>(ContextKeys.CiGreenMerge, out var green) && green is not null)
            return await GreenAsync(context, green, cancellationToken);
        if (series is not null && context is { Project: { } project, TicketId: { } ticket } && await series.HoldsTicketAsync(project, ticket, cancellationToken))
            return CommandResult.Ok("Not merged: a series holds this ticket, so the series master merges it.");
        if (context.Policy.When == MergeWhen.Manual) return CommandResult.Ok(Manual);
        if (!context.Pipeline.TryGet<IReadOnlyList<OpenedPullRequest>>(ContextKeys.GatePassedPullRequests, out var passed)
            || passed is null || passed.Count == 0)
            return CommandResult.Ok("Not merged: the run did not pass its gate with a code change on every repository.");
        if (context.SourceBranch is not { } branch)
            return CommandResult.Ok("Not merged: the run carries no work branch.");
        if (context.Policy.When == MergeWhen.OnCiGreen) return await AwaitCiAsync(context, passed, cancellationToken);
        var lines = await merges.RunAsync(context, passed, branch, attemptRunId: null, cancellationToken);
        return CommandResult.Ok($"merge: on_gate_green ({Mode(context.Policy)}) — {Joined(lines)}");
    }

    private const string Manual = "merge: manual — the pull requests stay open for review.";

    private async Task<CommandResult> GreenAsync(CompletePullRequestsContext context, CiGreenMergeRequest request, CancellationToken ct)
    {
        if (request.PullRequests.Any(p => p.NotStarted)) return await RequeueAsync(context, request, ct);
        if (request.PullRequests.Any(p => p.Rearm)) return await RearmAsync(context, request, ct);
        if (request.PullRequests.Any(p => p.NoCi))
            return CommandResult.Ok($"CI for the pull requests — {(ciStart is null ? "none started" : await ciStart.StartAsync(context, request, ct))}; nothing merges.");
        if (context.Policy.When == MergeWhen.Manual && request.AskedBy is null) return CommandResult.Ok(Manual);
        var entries = request.PullRequests.Select(p => new OpenedPullRequest(p.Repo, p.Url, OpenStatus.Opened)).ToList();
        var lines = await merges.RunAsync(context, entries, new BranchName(request.Branch), request.AttemptRunId, ct,
            asked: request.AskedBy is not null, judged: request.Judged);
        if (context.Pipeline.TryGet<ActSlot<IReadOnlyList<PullRequestMergeLine>>>(ContextKeys.ActOutcome, out var slot)) slot?.Set(lines);
        var why = request.AskedBy is { } by ? $"asked by {by}" : "merge: on_ci_green, CI green";
        return CommandResult.Ok($"{why} ({Mode(context.Policy)}) — {Joined(lines)}");
    }

    // 2026-11-24-629aa: each not-started pull request's required builds queued again; a repo not in the run queues nothing.
    private async Task<CommandResult> RequeueAsync(CompletePullRequestsContext context, CiGreenMergeRequest request, CancellationToken ct)
    {
        var said = new List<string>();
        foreach (var pr in request.PullRequests.Where(p => p.NotStarted)) said.Add($"{pr.Repo}: {await QueuedAsync(context, pr, ct)} queued");
        return CommandResult.Ok($"required builds that had not started, queued again — {string.Join("; ", said)}; nothing merges.");
    }

    // 2026-11-24-629ab: each pull request's auto-complete armed again, as the host answered. 2026-11-25-7a92c: GitLab merges at once when
    // the head's pipeline already passed; the answer says merged and the next state read records it, as for a person's merge.
    private async Task<CommandResult> RearmAsync(CompletePullRequestsContext context, CiGreenMergeRequest request, CancellationToken ct)
    {
        var said = new List<string>();
        foreach (var pr in request.PullRequests.Where(p => p.Rearm))
        {
            var answer = rearm is not null && context.Configs.FirstOrDefault(r => r.Name == pr.Repo) is { } repo
                ? await rearm.RearmAsync(repo, pr.Url, ct) : Providers.PullRequestCompletion.Refused("not armed, the repository is not in this run");
            said.Add($"{pr.Repo}: {answer.Outcome.ToString().ToLowerInvariant()}{(answer.Reason is { } why ? $" — {why}" : string.Empty)}");
        }
        return CommandResult.Ok($"auto-complete the host dropped at a conflict or a new head, armed again — {string.Join("; ", said)}");
    }

    // 2026-11-25-7a92b: CiBuildRequeue asks only for a head not started for merge.ci_settle_seconds, so the head is settled.
    private async Task<int> QueuedAsync(CompletePullRequestsContext context, CiGreenPullRequest pr, CancellationToken ct) =>
        requeue is not null && context.Configs.FirstOrDefault(r => r.Name == pr.Repo) is { } repo
            ? await requeue.RequeueAsync(repo, pr.Url, ct, headSettled: pr.NotStarted) : 0;

    // A pull request opened against a rung is left open, as a merge would leave it; the rest wait for CI.
    private async Task<CommandResult> AwaitCiAsync(
        CompletePullRequestsContext context, IReadOnlyList<OpenedPullRequest> passed, CancellationToken ct)
    {
        var waiting = new List<string>();
        foreach (var entry in passed)
            if (PullRequestMergeSequence.RungRefusal(context.Pipeline, entry.RepoName, context.SeriesBranch) is { } rung)
                await completer.PublishAsync(context.Pipeline, entry, Providers.PullRequestCompletion.Refused(rung), ct);
            else
            {
                await completer.PublishStatusAsync(context.Pipeline, entry, PullRequestStatuses.AwaitingCi, null, ct);
                waiting.Add(entry.RepoName);
            }
        return CommandResult.Ok(waiting.Count == 0 ? "merge: on_ci_green — every pull request was opened against a rung; left open."
            : $"merge: on_ci_green — {string.Join(", ", waiting)} wait for CI; agent-smith merges them once it is green on every one.");
    }

    private static string Joined(IReadOnlyList<PullRequestMergeLine> lines) => string.Join("; ", lines.Select(l => l.Text));

    private static string Mode(MergePolicyConfig policy) =>
        policy.RespectBranchProtection ? "respect_branch_protection: true" : "past branch protection";
}
