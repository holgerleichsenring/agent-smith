using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762g: what one run's spec review is read with — the <c>spec_review:</c> block and the
/// watchdog's wall-time ceiling the review's share and its first spec are measured against.</summary>
public sealed record SpecReviewSettings(SpecReviewConfig Review, int MaxRunWallTimeSeconds);
