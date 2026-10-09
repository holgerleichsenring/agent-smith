using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sweep;

/// <summary>
/// 2026-10-09-af10: a pull request whose sweep-launched reviews failed three times in a row, on any
/// heads, gets no more of them until a person asks again — a trusted <c>/agent-smith</c> command on
/// it, or closing and reopening it. The count lives on the pull request's row, so a restart keeps it;
/// the review that reaches the threshold says so once, on the pull request and in the log.
/// </summary>
public sealed class PrSweepBreaker(IServiceScopeFactory scopes, ISourceProviderFactory sources, ILogger<PrSweepBreaker> logger)
{
    public const int Threshold = 3;
    public const string PausedMarker = "<!-- agentsmith:pr-sweep-paused -->";

    public static bool Paused(PrSweepState row) => row.FailedReviews >= Threshold;

    /// <summary>What a sweep-launched review calls when it ends.</summary>
    public Func<bool, Task> Watch(SweepTarget target, string key, OpenPullRequest pr) => async succeeded =>
    {
        int failures;
        await using (var scope = scopes.CreateAsyncScope())
            failures = await scope.ServiceProvider.GetRequiredService<PrSweepStore>()
                .RecordReviewOutcomeAsync(key, pr.Number, succeeded, CancellationToken.None);
        if (failures == Threshold) await AnnounceAsync(target, pr);
    };

    private async Task AnnounceAsync(SweepTarget target, OpenPullRequest pr)
    {
        logger.LogWarning("PR sweep paused for {Repo}#{Pr}: the last {Count} reviews it started failed; "
            + "a trusted /agent-smith command on the pull request resumes it", target.Repo.Url, pr.Number, Threshold);
        if (sources.Create(target.Repo) is IPrCommentProvider comments)
            await comments.PostCommentAsync(pr.Number, Text, CancellationToken.None);
    }

    private const string Text = PausedMarker + "\n"
        + "agent-smith stopped reviewing this pull request on its own: the last 3 reviews it started here failed. "
        + "New commits will not start another one. Comment `/agent-smith pr-review` to ask for a review and resume, "
        + "or close and reopen the pull request.";
}
