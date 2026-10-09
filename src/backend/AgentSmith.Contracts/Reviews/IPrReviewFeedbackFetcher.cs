using AgentSmith.Contracts.Commands;

namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-10-08-e8b9d: reads the review on the previous attempt's open pull requests into
/// ContextKeys.PrReviewFeedback — fail-soft: a review that cannot be read costs the section, not the run.
/// </summary>
public interface IPrReviewFeedbackFetcher
{
    Task FetchAsync(PipelineContext pipeline, CancellationToken cancellationToken);
}
