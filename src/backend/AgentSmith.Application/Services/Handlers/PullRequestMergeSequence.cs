using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-3435b: merges a run's pull requests in order, extracted from 023fa's CompletePullRequestsHandler so a
/// code run (on_gate_green) and a merge-green run (on_ci_green) merge the same way: a pull request opened against a rung
/// is left open, the first refusal stops the rest, every outcome is recorded per repo. A merge-green run approves first
/// where the host needs it and records each outcome on the attempt whose pull request waited for CI as well.
/// </summary>
public sealed class PullRequestMergeSequence(
    PullRequestCompleter completer, PullRequestApprovalStep approvals, ILogger<PullRequestMergeSequence> logger)
{
    public async Task<IReadOnlyList<string>> RunAsync(CompletePullRequestsContext context,
        IReadOnlyList<OpenedPullRequest> entries, BranchName branch, string? attemptRunId, CancellationToken ct)
    {
        var lines = new List<string>();
        var stopped = false;
        foreach (var entry in entries)
        {
            if (stopped) { lines.Add($"{entry.RepoName}: not attempted, an earlier repository was not merged"); continue; }
            var (line, refused) = await OneAsync(context, entry, branch, attemptRunId, ct);
            lines.Add(line);
            stopped = refused;
        }
        return lines;
    }

    private async Task<(string Line, bool Refused)> OneAsync(CompletePullRequestsContext context, OpenedPullRequest entry,
        BranchName branch, string? attemptRunId, CancellationToken ct)
    {
        if (context.Configs.FirstOrDefault(r => r.Name == entry.RepoName) is not { } repo)
            return ($"{entry.RepoName}: not merged, the repository is not in this run", true);
        if (PullRequestTargets.For(context.Pipeline, repo.Name) is { } rung)
            return await RefuseAsync(context, entry, attemptRunId,
                $"opened against '{rung.Value}', and merging it would land it there rather than on the default branch; left open", ct);
        if (attemptRunId is not null
            && await approvals.ApproveAsync(repo, entry.Url!, context.Policy, ct) is { Outcome: PullRequestApprovalOutcome.Refused } no)
            return await RefuseAsync(context, entry, attemptRunId, no.Reason!, ct);
        var completion = await completer.CompleteAsync(context.Pipeline, repo, entry, branch, context.Policy.Protection, ct);
        if (attemptRunId is not null)
            await completer.PublishOnAsync(attemptRunId, entry, InitCompletionReport.StatusOf(completion), completion.Reason, ct);
        logger.LogInformation("{Repo}: merge {Outcome}", repo.Name, completion.Outcome);
        return completion.Outcome switch
        {
            PullRequestCompletionOutcome.Merged => ($"{entry.RepoName}: merged ({entry.Url})", false),
            PullRequestCompletionOutcome.Armed => ($"{entry.RepoName}: armed — {completion.Reason}", false),
            _ => ($"{entry.RepoName}: still open — {completion.Reason}", true),
        };
    }

    private async Task<(string, bool)> RefuseAsync(
        CompletePullRequestsContext context, OpenedPullRequest entry, string? attemptRunId, string reason, CancellationToken ct)
    {
        var refused = PullRequestCompletion.Refused(reason);
        await completer.PublishAsync(context.Pipeline, entry, refused, ct);
        if (attemptRunId is not null)
            await completer.PublishOnAsync(attemptRunId, entry, InitCompletionReport.StatusOf(refused), reason, ct);
        return ($"{entry.RepoName}: {reason}", true);
    }
}
