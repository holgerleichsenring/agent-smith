using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// 2026-10-08-e8b9c / 0781: the newest standing request for changes on a pull request after the
/// attempt's cutoff, by a person the host's trust admits — never a bot, never a system note. One
/// rule for the run's own read and for the rework worker.
/// <para>A comment command (<see cref="CommentCommandPrefix"/>) written by such a person after the
/// cutoff is an act beside the vote — never one of our own notes. The newer of the two is the act.</para>
/// </summary>
public sealed class PrStandingAct(IPrReviewAuthorTrust trust)
{
    public async Task<PrReviewNote?> NewestAsync(
        RepoType host, IPrReviewActReader reader, string url, PreviousAttempt attempt, CancellationToken cancellationToken)
    {
        PrReviewNote? newest = null;
        foreach (var request in await reader.ChangesRequestedAsync(url, cancellationToken))
            if (attempt.Precedes(request.At) && (newest is null || request.At > newest.At)
                && IsPerson(request) && await trust.IsTrustedAsync(host, request.Author!, cancellationToken))
                newest = request;
        return newest;
    }

    /// <summary>The newest command among the pull request's review, each candidate's author trust-checked.</summary>
    public async Task<PrReviewNote?> NewestCommandAsync(
        RepoType host, IPrReviewThreadReader reader, string url, PreviousAttempt attempt, CancellationToken cancellationToken)
    {
        var candidates = (await reader.ListAsync(url, cancellationToken)).SelectMany(t => t.Notes)
            .Where(n => IsCommand(n, attempt)).OrderByDescending(n => n.At);
        foreach (var note in candidates)
            if (await trust.IsTrustedAsync(host, note.Author!, cancellationToken))
                return note;
        return null;
    }

    /// <summary>The newest command among notes whose authors the caller already trusted.</summary>
    public static PrReviewNote? NewestCommand(IEnumerable<PrReviewThread> trustedThreads, PreviousAttempt attempt) =>
        trustedThreads.SelectMany(t => t.Notes).Where(n => IsCommand(n, attempt)).MaxBy(n => n.At);

    public static ReworkAct Act(PrReviewNote note) =>
        new(note.Author!.AuthorLogin, note.At, ReworkChannel.PullRequest);

    /// <summary>The newer of two acts; either may be absent.</summary>
    public static PrReviewNote? Newer(PrReviewNote? first, PrReviewNote? second) =>
        first is null || (second is not null && second.At > first.At) ? second : first;

    private static bool IsCommand(PrReviewNote note, PreviousAttempt attempt) =>
        attempt.Precedes(note.At) && IsPerson(note) && !note.IsOurs
        && !note.Body.StartsWith(OwnPrNoteMarker.Prefix, StringComparison.Ordinal)
        && CommentCommandPrefix.IsCommand(note.Body);

    private static bool IsPerson(PrReviewNote note) => !note.IsBot && !note.IsSystem && note.Author is not null;
}
