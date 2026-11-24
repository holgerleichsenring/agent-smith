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
/// <para>2026-11-17-3435b: with merge: on_ci_green the code run merges nothing: each gate-passed pull request is
/// recorded awaiting_ci, and the server's CI watch starts a merge-green run once CI is green on every one. That run
/// carries <see cref="ContextKeys.CiGreenMerge"/> and merges them here, approving first where the host needs it.</para>
/// <para>2026-11-23-31dce: a merge-green request a person asked for (its AskedBy) runs under merge: manual too — the click is
/// the manual merge; the CI watch's own request still does not. Each repo's outcome goes into the outcome slot when the
/// server seeded one.</para>
/// <para>2026-11-24-f9c5d: the CI loop's request for pull requests into a series branch that report no CI first starts their
/// CI (<see cref="PullRequestCiStart"/>), under merge.series_when manual too; the merge follows only when none has CI anywhere.</para>
/// </summary>
public sealed class CompletePullRequestsHandler(PullRequestMergeSequence merges, PullRequestCompleter completer, PullRequestCiStart? ciStart = null)
    : ICommandHandler<CompletePullRequestsContext>
{
    public async Task<CommandResult> ExecuteAsync(CompletePullRequestsContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var green = context.Pipeline.TryGet<CiGreenMergeRequest>(ContextKeys.CiGreenMerge, out var asked) ? asked : null;
        if (green is not null && ciStart is not null && await ciStart.StartAsync(context, green, cancellationToken) is { MayMerge: false } started)
            return CommandResult.Ok($"CI for the series pull requests — {started.Said}; nothing merges until it reports.");
        if (context.Policy.When == MergeWhen.Manual && green?.AskedBy is null)
            return CommandResult.Ok("merge: manual — the pull requests stay open for review.");
        if (green is not null) return await MergeGreenAsync(context, green, cancellationToken);
        if (!context.Pipeline.TryGet<IReadOnlyList<OpenedPullRequest>>(ContextKeys.GatePassedPullRequests, out var passed)
            || passed is null || passed.Count == 0)
            return CommandResult.Ok("Not merged: the run did not pass its gate with a code change on every repository.");
        if (context.SourceBranch is not { } branch)
            return CommandResult.Ok("Not merged: the run carries no work branch.");
        if (context.Policy.When == MergeWhen.OnCiGreen) return await AwaitCiAsync(context, passed, cancellationToken);
        var lines = await merges.RunAsync(context, passed, branch, attemptRunId: null, cancellationToken);
        return CommandResult.Ok($"merge: on_gate_green ({Mode(context.Policy)}) — {Joined(lines)}");
    }

    private async Task<CommandResult> MergeGreenAsync(
        CompletePullRequestsContext context, CiGreenMergeRequest request, CancellationToken ct)
    {
        var entries = request.PullRequests.Select(p => new OpenedPullRequest(p.Repo, p.Url, OpenStatus.Opened)).ToList();
        var lines = await merges.RunAsync(context, entries, new BranchName(request.Branch), request.AttemptRunId, ct,
            asked: request.AskedBy is not null);
        if (context.Pipeline.TryGet<ActSlot<IReadOnlyList<PullRequestMergeLine>>>(ContextKeys.ActOutcome, out var slot)) slot?.Set(lines);
        var why = request.AskedBy is { } by ? $"asked by {by}" : "merge: on_ci_green, CI green";
        return CommandResult.Ok($"{why} ({Mode(context.Policy)}) — {Joined(lines)}");
    }

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
