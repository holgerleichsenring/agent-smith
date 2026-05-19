using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services;

/// <summary>
/// Default <see cref="IFilterRoundBatcher"/>. Greedy bin-packing on JSON-serialized
/// observation size against a budget = maxOutputTokens * CharsPerToken * Reserve.
/// Reserve covers LLM verbosity + outer JSON envelope; chars-per-token is a naive
/// English-text proxy that is accurate enough at the reserve margin. Tighter
/// estimation (tiktoken) would be over-engineering for this use.
/// </summary>
public sealed class FilterRoundBatcher : IFilterRoundBatcher
{
    private const double BudgetReserve = 0.85;
    private const int CharsPerToken = 4;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public IReadOnlyList<IReadOnlyList<SkillObservation>> Split(
        IReadOnlyList<SkillObservation> observations, int maxOutputTokens)
    {
        if (observations.Count == 0) return [];

        var budgetChars = (int)(maxOutputTokens * CharsPerToken * BudgetReserve);
        var batches = new List<IReadOnlyList<SkillObservation>>();
        var current = new List<SkillObservation>();
        var currentChars = 0;

        foreach (var obs in observations)
        {
            var size = JsonSerializer.Serialize(obs, JsonOptions).Length;
            if (currentChars + size > budgetChars && current.Count > 0)
            {
                batches.Add(current);
                current = new List<SkillObservation>();
                currentChars = 0;
            }
            current.Add(obs);
            currentChars += size;
        }
        if (current.Count > 0) batches.Add(current);
        return batches;
    }
}
