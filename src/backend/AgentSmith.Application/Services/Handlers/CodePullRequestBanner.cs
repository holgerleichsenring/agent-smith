using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// The banner a coding run's pull request leads with. A red run's PR is a draft and says so at the top of the
/// body, so a reviewer sees "verification red" before the ticket text — not a change that looks ready.
/// 2026-09-07-f420: a FAILED run's draft names the failure instead; p0439: a delivered shortfall leads with what
/// it delivers and what it lacks; 2026-10-13-8762u: an attempt stopped at a series boundary says how far the
/// series is and where the next attempt continues. Moved out of CommitAndPRHandler, which composes it twice.
/// </summary>
public sealed class CodePullRequestBanner(
    ShortfallDelivery shortfallDelivery, FailedRunPersistence failedRun, SeriesChunkStop chunkStop)
{
    public string For(string? failureReason, RunShortfall? shortfall, bool isDraft, SpecSequenceProgress? progress) =>
        shortfall is not null ? shortfallDelivery.PullRequestBanner(shortfall)
        : failureReason is not null ? failedRun.PullRequestBanner(failureReason)
        : progress?.StoppedAtChunk == true ? chunkStop.PullRequestBanner(progress)
        : isDraft
            ? "> ⚠️ **Verification red** — build/tests did not pass. Draft for review, do not merge as-is.\n\n"
            : string.Empty;
}
