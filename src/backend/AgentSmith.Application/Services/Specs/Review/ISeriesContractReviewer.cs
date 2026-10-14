using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762k: one fresh reviewer checks a whole set against its series contract, on the look its caller
/// opened. A port, because it is a model call the framework makes on its own behalf. A failed call, an unreadable
/// answer, or a run with no look is a review not taken, returned with its reason; it never fails its caller.
/// </summary>
public interface ISeriesContractReviewer
{
    Task<SeriesContractReview> ReviewAsync(
        SeriesContractInput input, DerivationLook? look, AgentConfig agent, PipelineCostTracker costTracker,
        CancellationToken cancellationToken);
}
