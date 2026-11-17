using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-023fa: a green code run merges its own pull requests when the project says merge: on_gate_green — the
/// operator's "merge when the run's own gate is green and fix forward". Only the pull requests CommitAndPR published
/// as gate-passed are merged, in order, in the project's protection mode; the first refusal stops the rest, so a
/// multi-repository change lands as far as it can and says where it stopped. A pull request opened against a rung is
/// left open: merging it lands it on the rung, not where the ticket's work belongs. Every outcome is recorded per repo
/// and said in the step result; nothing here fails the run, whose work is committed and open either way.
/// </summary>
public sealed class CompletePullRequestsHandler(
    PullRequestCompleter completer, ILogger<CompletePullRequestsHandler> logger)
    : ICommandHandler<CompletePullRequestsContext>
{
    public async Task<CommandResult> ExecuteAsync(CompletePullRequestsContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Policy.When != MergeWhen.OnGateGreen)
            return CommandResult.Ok("merge: manual — the pull requests stay open for review.");
        if (!context.Pipeline.TryGet<IReadOnlyList<OpenedPullRequest>>(ContextKeys.GatePassedPullRequests, out var passed)
            || passed is null || passed.Count == 0)
            return CommandResult.Ok("Not merged: the run did not pass its gate with a code change on every repository.");
        if (context.SourceBranch is not { } branch)
            return CommandResult.Ok("Not merged: the run carries no work branch.");

        var lines = new List<string>();
        var stopped = false;
        foreach (var entry in passed)
        {
            if (stopped)
            {
                lines.Add($"{entry.RepoName}: not attempted, an earlier repository was not merged");
                continue;
            }
            var (line, refused) = await OneAsync(context, entry, branch, cancellationToken);
            lines.Add(line);
            stopped = refused;
        }
        return CommandResult.Ok($"merge: on_gate_green ({Mode(context.Policy)}) — {string.Join("; ", lines)}");
    }

    private async Task<(string Line, bool Refused)> OneAsync(
        CompletePullRequestsContext context, OpenedPullRequest entry, Domain.Models.BranchName branch, CancellationToken ct)
    {
        if (context.Configs.FirstOrDefault(r => r.Name == entry.RepoName) is not { } repo)
            return ($"{entry.RepoName}: not merged, the repository is not in this run", true);
        if (PullRequestTargets.For(context.Pipeline, repo.Name) is { } rung)
        {
            var reason = $"opened against '{rung.Value}', and merging it would land it there rather than on the default branch; left open";
            await completer.PublishAsync(context.Pipeline, entry, PullRequestCompletion.Refused(reason), ct);
            return ($"{entry.RepoName}: {reason}", true);
        }
        var completion = await completer.CompleteAsync(
            context.Pipeline, repo, entry, branch, context.Policy.Protection, ct);
        logger.LogInformation("{Repo}: merge {Outcome}", repo.Name, completion.Outcome);
        return completion.Outcome switch
        {
            PullRequestCompletionOutcome.Merged => ($"{entry.RepoName}: merged ({entry.Url})", false),
            PullRequestCompletionOutcome.Armed => ($"{entry.RepoName}: armed — {completion.Reason}", false),
            _ => ($"{entry.RepoName}: still open — {completion.Reason}", true),
        };
    }

    private static string Mode(MergePolicyConfig policy) =>
        policy.RespectBranchProtection ? "respect_branch_protection: true" : "past branch protection";
}
