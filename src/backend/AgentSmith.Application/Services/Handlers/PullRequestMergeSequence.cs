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
/// <para>2026-11-23-31dce: on a merge-green run each pull request's base is read from the host first and one off the default
/// branch is refused (<see cref="PullRequestBaseGuard"/>) and, like an unlanded requirement, keeps the attempt's pull request
/// awaiting CI; every repo answers a <see cref="PullRequestMergeLine"/>. A merge a person asked for (<paramref name="asked"/>)
/// marks a refusal on its own run only, so a click never ends the attempt's wait for the CI merge.</para>
/// </summary>
public sealed class PullRequestMergeSequence(
    PullRequestCompleter completer, PullRequestApprovalStep approvals, PullRequestBaseGuard bases,
    ILogger<PullRequestMergeSequence> logger, UnmetTicketRequirements? requirements = null)
{
    public async Task<IReadOnlyList<PullRequestMergeLine>> RunAsync(CompletePullRequestsContext context,
        IReadOnlyList<OpenedPullRequest> entries, BranchName branch, string? attemptRunId, CancellationToken ct, bool asked = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        var mark = asked ? null : attemptRunId; // the attempt a refusal is marked on
        if (await UnlandedAsync(context, ct) is { } unlanded) return await RefuseEachAsync(context, entries, mark, unlanded, ct);
        var lines = new List<PullRequestMergeLine>();
        foreach (var entry in entries)
            lines.Add(lines.Any(l => l.Outcome == PullRequestCompletionOutcome.Refused)
                ? Refused(entry, "not attempted, an earlier repository was not merged")
                : await OneAsync(context, entry, branch, attemptRunId, mark, ct));
        return lines;
    }

    private async Task<PullRequestMergeLine> OneAsync(CompletePullRequestsContext context, OpenedPullRequest entry,
        BranchName branch, string? attemptRunId, string? mark, CancellationToken ct)
    {
        if (context.Configs.FirstOrDefault(r => r.Name == entry.RepoName) is not { } repo)
            return Refused(entry, "not merged, the repository is not in this run");
        if (RungRefusal(context.Pipeline, repo.Name) is { } rung) return await RefuseAsync(context, entry, mark, rung, ct);
        if (attemptRunId is not null && await bases.RefusalAsync(repo, entry.Url!, ct) is { } offMain)
            return await HoldAsync(context, entry, mark, offMain, ct);
        if (attemptRunId is not null
            && await approvals.ApproveAsync(repo, entry.Url!, context.Policy, ct) is { Outcome: PullRequestApprovalOutcome.Refused } no)
            return await RefuseAsync(context, entry, mark, no.Reason!, ct);
        var completion = await completer.CompleteAsync(context.Pipeline, repo, entry, branch, context.Policy.Protection, ct);
        if ((completion.Outcome == PullRequestCompletionOutcome.Refused ? mark : attemptRunId) is { } on)
            await completer.PublishOnAsync(on, entry, InitCompletionReport.StatusOf(completion), completion.Reason, ct);
        logger.LogInformation("{Repo}: merge {Outcome}", repo.Name, completion.Outcome);
        return new(entry.RepoName, completion.Outcome, completion.Outcome switch
        {
            PullRequestCompletionOutcome.Merged => $"{entry.RepoName}: merged ({entry.Url})",
            PullRequestCompletionOutcome.Armed => $"{entry.RepoName}: armed — {completion.Reason}",
            _ => $"{entry.RepoName}: still open — {completion.Reason}",
        });
    }

    private static PullRequestMergeLine Refused(OpenedPullRequest entry, string reason) =>
        new(entry.RepoName, PullRequestCompletionOutcome.Refused, $"{entry.RepoName}: {reason}");

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

    private async Task<IReadOnlyList<PullRequestMergeLine>> RefuseEachAsync(CompletePullRequestsContext context,
        IReadOnlyList<OpenedPullRequest> entries, string? attemptRunId, string unlanded, CancellationToken ct)
    {
        var lines = new List<PullRequestMergeLine>();
        foreach (var entry in entries)
            lines.Add(RungRefusal(context.Pipeline, entry.RepoName) is { } rung
                ? await RefuseAsync(context, entry, attemptRunId, rung, ct)
                : await HoldAsync(context, entry, attemptRunId, unlanded, ct));
        return lines;
    }

    // Refused on this run; the attempt's pull request keeps awaiting CI, so the CI merge asks again once the ticket landed.
    private async Task<PullRequestMergeLine> HoldAsync(
        CompletePullRequestsContext context, OpenedPullRequest entry, string? attemptRunId, string reason, CancellationToken ct)
    {
        await completer.PublishAsync(context.Pipeline, entry, PullRequestCompletion.Refused(reason), ct);
        if (attemptRunId is not null) await completer.PublishOnAsync(attemptRunId, entry, PullRequestStatuses.AwaitingCi, reason, ct);
        return Refused(entry, reason);
    }

    private async Task<PullRequestMergeLine> RefuseAsync(
        CompletePullRequestsContext context, OpenedPullRequest entry, string? attemptRunId, string reason, CancellationToken ct)
    {
        var refused = PullRequestCompletion.Refused(reason);
        await completer.PublishAsync(context.Pipeline, entry, refused, ct);
        if (attemptRunId is not null)
            await completer.PublishOnAsync(attemptRunId, entry, InitCompletionReport.StatusOf(refused), reason, ct);
        return Refused(entry, reason);
    }
}
