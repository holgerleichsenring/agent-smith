using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-10-18cf: the spec_review settings a design turn reviews with. 2026-11-10-8e59: an invalid block is refused
/// at validation, so a turn meets one only when the store cannot be read or validation was bypassed; it then
/// reviews nothing and marks what it proposed with <see cref="ProposalReviewSettingsRead.Problem"/>, and the review
/// before building still attacks it. Never clamped to defaults. Spec proposals and ticket plans read it alike.
/// </summary>
internal sealed class ProposalReviewSettingsReader(ISpecReviewSettingsReader reader, ILogger logger)
{
    public ProposalReviewSettingsRead Read()
    {
        try
        {
            var current = reader.Current();
            if (current.Review.Problems() is not { Count: > 0 } problems) return new(current, null);
            var problem = string.Join("; ", problems);
            logger.LogWarning("The spec_review settings are invalid ({Problems}); the proposal is not reviewed", problem);
            return new(null, problem);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The spec_review settings could not be read; the proposal is not reviewed");
            return new(null, $"the spec_review settings could not be read: {ex.Message}");
        }
    }
}

/// <summary>The settings, or the problem that stops a design turn's review.</summary>
internal sealed record ProposalReviewSettingsRead(SpecReviewSettings? Settings, string? Problem);
