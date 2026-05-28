using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0176b: model-name → ModelPricing lookup. Extracted from
/// PipelineCostTracker so the LlmCallFinished event producer
/// (EventPublishingChatClient) can compute per-call cost without
/// pulling in the tracker's aggregation surface. Single source of
/// truth for the default model → pricing map; project-level pricing
/// overrides layer on top inside the tracker.
/// </summary>
public interface IModelPricingResolver
{
    /// <summary>
    /// Returns pricing for the given model id, or null when no entry
    /// matches. Implementations must accept date-suffixed provider ids
    /// (e.g. <c>gpt-4.1-2025-04-14</c>) and fall back to longest-prefix
    /// match against the registered base names.
    /// </summary>
    ModelPricing? Resolve(string model);
}
