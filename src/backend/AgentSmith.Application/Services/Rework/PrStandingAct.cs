using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// 2026-10-08-e8b9c / 0781: the newest standing request for changes on a pull request after the
/// attempt's cutoff, by a person the host's trust admits — never a bot, never a system note. One
/// rule for the run's own read and for the rework worker.
/// </summary>
public sealed class PrStandingAct(IPrReviewAuthorTrust trust)
{
    public async Task<PrReviewNote?> NewestAsync(
        RepoType host, IPrReviewActReader reader, string url, PreviousAttempt attempt, CancellationToken cancellationToken)
    {
        PrReviewNote? newest = null;
        foreach (var request in await reader.ChangesRequestedAsync(url, cancellationToken))
            if (attempt.Precedes(request.At) && (newest is null || request.At > newest.At)
                && !request.IsBot && !request.IsSystem && request.Author is not null
                && await trust.IsTrustedAsync(host, request.Author, cancellationToken))
                newest = request;
        return newest;
    }

    public static ReworkAct Act(PrReviewNote note) =>
        new(note.Author!.AuthorLogin, note.At, ReworkChannel.PullRequest);
}
