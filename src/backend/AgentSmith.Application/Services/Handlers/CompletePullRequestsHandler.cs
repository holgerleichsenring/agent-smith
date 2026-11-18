using AgentSmith.Application.Models;
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
/// </summary>
public sealed class CompletePullRequestsHandler(PullRequestMergeSequence merges, PullRequestCompleter completer)
    : ICommandHandler<CompletePullRequestsContext>
{
    public async Task<CommandResult> ExecuteAsync(CompletePullRequestsContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Policy.When == MergeWhen.Manual)
            return CommandResult.Ok("merge: manual — the pull requests stay open for review.");
        if (context.Pipeline.TryGet<CiGreenMergeRequest>(ContextKeys.CiGreenMerge, out var green) && green is not null)
            return await MergeGreenAsync(context, green, cancellationToken);
        if (!context.Pipeline.TryGet<IReadOnlyList<OpenedPullRequest>>(ContextKeys.GatePassedPullRequests, out var passed)
            || passed is null || passed.Count == 0)
            return CommandResult.Ok("Not merged: the run did not pass its gate with a code change on every repository.");
        if (context.SourceBranch is not { } branch)
            return CommandResult.Ok("Not merged: the run carries no work branch.");
        if (context.Policy.When == MergeWhen.OnCiGreen) return await AwaitCiAsync(context, passed, cancellationToken);
        var lines = await merges.RunAsync(context, passed, branch, attemptRunId: null, cancellationToken);
        return CommandResult.Ok($"merge: on_gate_green ({Mode(context.Policy)}) — {string.Join("; ", lines)}");
    }

    private async Task<CommandResult> MergeGreenAsync(
        CompletePullRequestsContext context, CiGreenMergeRequest request, CancellationToken ct)
    {
        var entries = request.PullRequests.Select(p => new OpenedPullRequest(p.Repo, p.Url, OpenStatus.Opened)).ToList();
        var lines = await merges.RunAsync(context, entries, new BranchName(request.Branch), request.AttemptRunId, ct);
        return CommandResult.Ok($"merge: on_ci_green, CI green ({Mode(context.Policy)}) — {string.Join("; ", lines)}");
    }

    // A pull request opened against a rung is left open, as a merge would leave it; the rest wait for CI.
    private async Task<CommandResult> AwaitCiAsync(
        CompletePullRequestsContext context, IReadOnlyList<OpenedPullRequest> passed, CancellationToken ct)
    {
        var waiting = new List<string>();
        foreach (var entry in passed)
            if (PullRequestTargets.For(context.Pipeline, entry.RepoName) is { } rung)
                await completer.PublishAsync(context.Pipeline, entry, Providers.PullRequestCompletion.Refused(
                    $"opened against '{rung.Value}', and merging it would land it there rather than on the default branch; left open"), ct);
            else
            {
                await completer.PublishStatusAsync(context.Pipeline, entry, PullRequestStatuses.AwaitingCi, null, ct);
                waiting.Add(entry.RepoName);
            }
        return CommandResult.Ok(waiting.Count == 0 ? "merge: on_ci_green — every pull request was opened against a rung; left open."
            : $"merge: on_ci_green — {string.Join(", ", waiting)} wait for CI; agent-smith merges them once it is green on every one.");
    }

    private static string Mode(MergePolicyConfig policy) =>
        policy.RespectBranchProtection ? "respect_branch_protection: true" : "past branch protection";
}
