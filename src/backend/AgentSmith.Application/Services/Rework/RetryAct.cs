using AgentSmith.Contracts.Runs;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// 2026-10-13-9a59n: a stored Retry of an attempt as an act on its ticket. The request is a row of the run it
/// retries, so it is read off that attempt: it counts while it may be after what the attempt read — an attempt
/// that started after it has a newer cutoff and is no longer the attempt it was asked of.
/// </summary>
public static class RetryAct
{
    public static ReworkAct? Of(PreviousAttempt? attempt) =>
        attempt is { RetryRequestedBy: { } by, RetryRequestedAt: { } at } && attempt.Precedes(at)
            ? new ReworkAct(by, at, ReworkChannel.Retry)
            : null;
}
