using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-3435b: merges a run's pull requests in order, extracted from 023fa's CompletePullRequestsHandler so a
/// code run (on_gate_green) and a merge-green run (on_ci_green) merge the same way: a pull request opened against a rung
/// is left open, the first refusal stops the rest, every outcome is recorded per repo. A merge-green run approves first
/// where the host needs it and records each outcome on the attempt whose pull request waited for CI as well.
/// <para>2026-11-23-97c2a: no path lands a ticket before a ticket it requires — while one has not landed, every pull request
/// is refused (a rung's own refusal still names its rung), whichever path asked; a merge-green run's attempt keeps its pull
/// requests awaiting CI, so the CI merge asks again once the required ticket landed. Without the requirement reader (a process
/// with no ticket store) there are no requirements to read.</para>
/// </summary>
public sealed class PullRequestMergeSequence(
    PullRequestCompleter completer, PullRequestApprovalStep approvals, ILogger<PullRequestMergeSequence> logger,
    UnmetTicketRequirements? requirements = null)
{
    public async Task<IReadOnlyList<string>> RunAsync(CompletePullRequestsContext context,
        IReadOnlyList<OpenedPullRequest> entries, BranchName branch, string? attemptRunId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (await UnlandedAsync(context, ct) is { } unlanded) return await RefuseEachAsync(context, entries, attemptRunId, unlanded, ct);
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
        if (RungRefusal(context.Pipeline, repo.Name) is { } rung) return await RefuseAsync(context, entry, attemptRunId, rung, ct);
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

    /// <summary>Why a pull request opened against a rung is left open; null when it targets the default branch.</summary>
    internal static string? RungRefusal(PipelineContext pipeline, string repo) =>
        PullRequestTargets.For(pipeline, repo) is { } rung
            ? $"opened against '{rung.Value}', and merging it would land it there rather than on the default branch; left open"
            : null;

    // The refusal while a required ticket has not landed; null when every one landed or the ticket requires none.
    private async Task<string?> UnlandedAsync(CompletePullRequestsContext context, CancellationToken ct)
    {
        if (requirements is null || context.Tracker is null || context.TicketKey is null) return null;
        var (_, unmet) = await requirements.OfAsync(context.Tracker, context.TicketKey, ct);
        if (unmet.Count == 0) return null;
        var named = unmet.Select(e => e.RequiredTicketId).Distinct(StringComparer.Ordinal).Select(RequiresUnmetGate.Named).ToList();
        logger.LogInformation("{Ticket}: not merged, {Required} it requires has not landed", context.TicketKey, string.Join(", ", named));
        return $"not merged: {RequiresUnmetGate.MergesNamed(named)} it requires {(named.Count == 1 ? "has" : "have")} not landed";
    }

    private async Task<IReadOnlyList<string>> RefuseEachAsync(CompletePullRequestsContext context,
        IReadOnlyList<OpenedPullRequest> entries, string? attemptRunId, string unlanded, CancellationToken ct)
    {
        var lines = new List<string>();
        foreach (var entry in entries)
            lines.Add(RungRefusal(context.Pipeline, entry.RepoName) is { } rung
                ? (await RefuseAsync(context, entry, attemptRunId, rung, ct)).Item1
                : await HoldAsync(context, entry, attemptRunId, unlanded, ct));
        return lines;
    }

    // Refused on this run; the attempt's pull request keeps awaiting CI, so the CI merge asks again once the ticket landed.
    private async Task<string> HoldAsync(
        CompletePullRequestsContext context, OpenedPullRequest entry, string? attemptRunId, string reason, CancellationToken ct)
    {
        await completer.PublishAsync(context.Pipeline, entry, PullRequestCompletion.Refused(reason), ct);
        if (attemptRunId is not null) await completer.PublishOnAsync(attemptRunId, entry, PullRequestStatuses.AwaitingCi, reason, ct);
        return $"{entry.RepoName}: {reason}";
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
