using System.Text.Json;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Splits a SkillObservation list into batches that fit a token-budget ceiling.
/// Sizing is deterministic: with the per-field caps from ObservationCaps, the JSON-
/// serialized worst-case observation is bounded (~1500 chars), so the batcher
/// produces predictable batch counts. Used by FilterRoundHandler to keep filter
/// LLM calls under their max_output_tokens limit without truncation.
/// </summary>
internal static class TokenBudgetBatcher
{
    /// <summary>
    /// Reserve fraction. 15% headroom for LLM verbosity (preambles, expanded
    /// explanations) and the response's outer JSON envelope.
    /// </summary>
    private const double BudgetReserve = 0.85;

    /// <summary>
    /// Naive chars-per-token proxy for English-ish text. SkillObservation JSON is
    /// English-ish (field keys, description prose, severity strings), so this is
    /// accurate enough at the 0.85-reserve margin. Tighter estimation (tiktoken)
    /// would be over-engineering.
    /// </summary>
    private const int CharsPerToken = 4;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    internal static List<List<SkillObservation>> Split(
        IReadOnlyList<SkillObservation> observations, int maxOutputTokens)
    {
        if (observations.Count == 0) return [];

        var budgetChars = (int)(maxOutputTokens * CharsPerToken * BudgetReserve);
        var batches = new List<List<SkillObservation>>();
        var current = new List<SkillObservation>();
        var currentChars = 0;

        foreach (var obs in observations)
        {
            var size = JsonSerializer.Serialize(obs, JsonOptions).Length;
            if (currentChars + size > budgetChars && current.Count > 0)
            {
                batches.Add(current);
                current = [];
                currentChars = 0;
            }
            current.Add(obs);
            currentChars += size;
        }
        if (current.Count > 0) batches.Add(current);
        return batches;
    }
}
