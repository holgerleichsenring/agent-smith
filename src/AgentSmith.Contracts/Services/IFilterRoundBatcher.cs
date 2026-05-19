using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Splits a SkillObservation list into batches that fit a model's
/// max_output_tokens budget. List-mode FilterRoundHandler dispatches one LLM
/// call per batch so any observation count works without truncation. Sizing is
/// deterministic — per-field caps from ObservationCaps bound the per-observation
/// JSON size, so batch counts are predictable for a given input.
/// </summary>
public interface IFilterRoundBatcher
{
    /// <summary>
    /// Splits <paramref name="observations"/> into batches whose serialized JSON
    /// size fits under a fraction of <paramref name="maxOutputTokens"/> (the
    /// budget headroom covers LLM-side verbosity + envelope). Empty input
    /// returns an empty list.
    /// </summary>
    IReadOnlyList<IReadOnlyList<SkillObservation>> Split(
        IReadOnlyList<SkillObservation> observations, int maxOutputTokens);
}
