using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762g: the <c>spec_review:</c> block as one run reads it. 2026-11-19-3e10: the wall-time ceiling
/// went with the review budget it measured.</summary>
public sealed record SpecReviewSettings(SpecReviewConfig Review);
