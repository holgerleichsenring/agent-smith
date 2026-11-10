using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-10-18cf: the spec_review settings a design turn reviews with — the configured ones, or the defaults
/// when they cannot be read or are invalid, so a broken block never leaves a proposal with less than a full review.
/// A coding run reads the same block through <see cref="ISpecReviewSettingsReader"/> and refuses what is invalid.
/// </summary>
internal sealed class ProposalReviewSettingsReader(ISpecReviewSettingsReader reader, ILogger logger)
{
    public SpecReviewSettings Read()
    {
        try
        {
            var current = reader.Current();
            if (current.Review.Problems() is not { Count: > 0 } problems) return current;
            logger.LogWarning("The spec_review settings are invalid ({Problems}); the proposal is reviewed with the defaults",
                string.Join("; ", problems));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The spec_review settings could not be read; the proposal is reviewed with the defaults");
        }
        return new SpecReviewSettings(new SpecReviewConfig(), new OrchestratorGlobalConfig().MaxRunWallTimeSeconds);
    }
}
