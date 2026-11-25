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
/// 2026-11-17-3435b: merges a run's pull requests in order, extracted from 023fa's CompletePullRequestsHandler so a code run
/// and a merge-green run merge the same way (2026-11-25-9992i: only the merge-green run merges now): a pull request opened against a rung is left open, the
/// first refusal stops the rest, every outcome is recorded per repo (on a merge-green run, on the attempt that waited for CI too).
/// <para>2026-11-23-97c2a: while a required ticket has not landed every pull request is refused and the attempt keeps awaiting
/// CI. 2026-11-23-31dce: each base is read from the host and one off the default branch is refused (<see cref="PullRequestBaseGuard"/>);
/// an asked merge marks a refusal on its own run only. 2026-11-25-9992e: a judged merge (a person's from the series page) is held
/// by none of these. 2026-11-25-96aeb: the series master's merge is checked — an unlanded prerequisite (named with where it has
/// to land) and an unreadable base are refusals its override passes, the line naming what it went past; a base off the series
/// and default branch stays refused (<see cref="PullRequestMergeAsk.ChecksTarget"/>).</para>
/// <para>2026-11-23-8d16: an asked merge takes each out of draft and (0219) queues its not-started builds first; nothing
/// approves (9992h).</para>
/// </summary>
public sealed class PullRequestMergeSequence(
    PullRequestCompleter completer, PullRequestBaseGuard bases,
    ILogger<PullRequestMergeSequence> logger, UnmetTicketRequirements? requirements = null, PullRequestDraftStep? drafts = null, PullRequestBuildRequeue? requeue = null)
{
    public async Task<IReadOnlyList<PullRequestMergeLine>> RunAsync(CompletePullRequestsContext context,
        IReadOnlyList<OpenedPullRequest> entries, BranchName branch, string? attemptRunId, CancellationToken ct, PullRequestMergeAsk? ask = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ask ??= PullRequestMergeAsk.Unasked;
        var mark = ask.Asked ? null : attemptRunId; // the attempt a refusal is marked on
        var unlanded = ask.Judged && !ask.ChecksTarget ? null : await UnlandedAsync(context, ask, ct); // a person's merge reads none
        if (!ask.Judged && unlanded is not null) return await RefuseEachAsync(context, entries, mark, unlanded, ct);
        var lines = new List<PullRequestMergeLine>();
        foreach (var entry in entries)
            lines.Add(lines.Any(l => l.Outcome == PullRequestCompletionOutcome.Refused)
                ? PullRequestMergeLine.Refused(entry.RepoName, "not attempted, an earlier repository was not merged")
                : await OneAsync(context, entry, branch, attemptRunId, mark, ask, unlanded, ct));
        return lines;
    }

    private async Task<PullRequestMergeLine> OneAsync(CompletePullRequestsContext context, OpenedPullRequest entry,
        BranchName branch, string? attemptRunId, string? mark, PullRequestMergeAsk ask, string? unlanded, CancellationToken ct)
    {
        if (context.Configs.FirstOrDefault(r => r.Name == entry.RepoName) is not { } repo)
            return PullRequestMergeLine.Refused(entry.RepoName, "not merged, the repository is not in this run");
        if (!ask.Judged && RungRefusal(context.Pipeline, repo.Name, context.SeriesBranch) is { } rung) return await RefuseAsync(context, entry, mark, rung, ct);
        var target = attemptRunId is null || (ask.Judged && !ask.ChecksTarget) ? null : await bases.RefusalAsync(repo, entry.Url!, ct, context.SeriesBranch);
        if (target is not null && (!ask.Judged || !PullRequestBaseGuard.IsUnread(target)))
            return await HoldAsync(context, entry, mark, target, ct) with { Overridable = PullRequestBaseGuard.IsUnread(target) };
        if (ask.Asked && drafts is not null && await drafts.RefusalAsync(repo, entry.Url!, ct) is { } draft)
            return await RefuseAsync(context, entry, mark, draft, ct);
        if (ask.Asked && requeue is not null) await requeue.RequeueAsync(repo, entry.Url!, ct);
        return await CompleteAsync(context, repo, entry, branch, (attemptRunId, mark), ask, [.. new[] { unlanded, target }.OfType<string>()], ct);
    }

    // The merge itself; a judged one names the refusals it went past (2026-11-25-96aeb).
    private async Task<PullRequestMergeLine> CompleteAsync(CompletePullRequestsContext context, RepoConnection repo, OpenedPullRequest entry,
        BranchName branch, (string? Attempt, string? Mark) on, PullRequestMergeAsk ask, IReadOnlyList<string> passed, CancellationToken ct)
    {
        var completion = await completer.CompleteAsync(context.Pipeline, repo, entry, branch, context.Protection, context.Style, ct);
        if ((completion.Outcome == PullRequestCompletionOutcome.Refused ? on.Mark : on.Attempt) is { } run)
            await completer.PublishOnAsync(run, entry, InitCompletionReport.StatusOf(completion), completion.Reason, ct);
        logger.LogInformation("{Repo}: merge {Outcome}{Past}", repo.Name, completion.Outcome, passed.Count == 0 ? string.Empty
            : $" past {string.Join("; ", passed)} (override: {ask.OverrideReason ?? "a person's merge"})");
        return PullRequestMergeLine.Of(entry.RepoName, entry.Url, completion)
            with { Overrode = completion.Outcome == PullRequestCompletionOutcome.Refused ? [] : passed };
    }

    /// <summary>Why a pull request opened against a rung is left open; null for the default branch or the series branch (f9c5c).</summary>
    internal static string? RungRefusal(PipelineContext pipeline, string repo, string? seriesBranch = null) =>
        PullRequestTargets.For(pipeline, repo) is { } rung && !string.Equals(rung.Value, seriesBranch, StringComparison.Ordinal)
            ? $"opened against '{rung.Value}', and merging it would land it there rather than on the default branch; left open"
            : null;

    // The refusal while a required ticket has not landed; null when every one landed or the ticket requires none.
    private async Task<string?> UnlandedAsync(CompletePullRequestsContext context, PullRequestMergeAsk ask, CancellationToken ct)
    {
        if (requirements is null || context.Tracker is null || context.TicketKey is null) return null;
        var (_, unmet) = await requirements.OfAsync(context.Tracker, context.TicketKey, ct);
        if (unmet.Count == 0) return null;
        var refusal = PullRequestPrerequisiteWords.Refusal(unmet, context.SeriesBranch, ask.Members);
        logger.LogInformation("{Ticket}: {Refusal}", context.TicketKey, refusal);
        return refusal;
    }

    private async Task<IReadOnlyList<PullRequestMergeLine>> RefuseEachAsync(CompletePullRequestsContext context,
        IReadOnlyList<OpenedPullRequest> entries, string? attemptRunId, string unlanded, CancellationToken ct)
    {
        var lines = new List<PullRequestMergeLine>();
        foreach (var entry in entries)
            lines.Add(RungRefusal(context.Pipeline, entry.RepoName, context.SeriesBranch) is { } rung
                ? await RefuseAsync(context, entry, attemptRunId, rung, ct)
                : await HoldAsync(context, entry, attemptRunId, unlanded, ct) with { Overridable = true });
        return lines;
    }

    // Refused on this run; the attempt's pull request keeps awaiting CI, so the CI merge asks again once the ticket landed.
    private async Task<PullRequestMergeLine> HoldAsync(
        CompletePullRequestsContext context, OpenedPullRequest entry, string? attemptRunId, string reason, CancellationToken ct)
    {
        await completer.PublishAsync(context.Pipeline, entry, PullRequestCompletion.Refused(reason), ct);
        if (attemptRunId is not null) await completer.PublishOnAsync(attemptRunId, entry, PullRequestStatuses.AwaitingCi, reason, ct);
        return PullRequestMergeLine.Refused(entry.RepoName, reason);
    }

    private async Task<PullRequestMergeLine> RefuseAsync(
        CompletePullRequestsContext context, OpenedPullRequest entry, string? attemptRunId, string reason, CancellationToken ct)
    {
        var refused = PullRequestCompletion.Refused(reason);
        await completer.PublishAsync(context.Pipeline, entry, refused, ct);
        if (attemptRunId is not null)
            await completer.PublishOnAsync(attemptRunId, entry, InitCompletionReport.StatusOf(refused), reason, ct);
        return PullRequestMergeLine.Refused(entry.RepoName, reason);
    }
}
