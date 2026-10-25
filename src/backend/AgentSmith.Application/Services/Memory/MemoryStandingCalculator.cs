using AgentSmith.Contracts.Memory;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-10-25-9324d: a repository's evidence turned into each memory's standing. Every fact's weight fades with
/// a half-life of <see cref="MemoryStandingWeights.HalfLifeDays"/>, so what the runs said lately counts most and an
/// old verdict is never final. A reliance is no evidence by itself and is not counted.
/// </summary>
public sealed class MemoryStandingCalculator
{
    public IReadOnlyDictionary<string, MemoryStanding> Of(IReadOnlyList<MemoryEvidenceFact> facts, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts.Where(f => f.Kind != MemoryEvidenceKinds.Relied)
            .GroupBy(f => f.Slug, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => Standing(g.Key, [.. g], now), StringComparer.Ordinal);
    }

    private static MemoryStanding Standing(string slug, IReadOnlyList<MemoryEvidenceFact> facts, DateTimeOffset now)
    {
        var sum = facts.Sum(f => f.Weight * Math.Pow(0.5, Math.Max(0, (now - f.At).TotalDays) / MemoryStandingWeights.HalfLifeDays));
        var contradicted = facts.Count(f => f.Kind == MemoryEvidenceKinds.Contradicted);
        var label = contradicted > 0 && sum < 0 ? MemoryStandingLabels.Disputed
            : sum > 0 ? MemoryStandingLabels.Holding : MemoryStandingLabels.Unconfirmed;
        return new MemoryStanding(slug, facts.Count(f => f.Kind == MemoryEvidenceKinds.Confirmed),
            facts.Count(f => f.Kind == MemoryEvidenceKinds.Strained), contradicted, facts.Max(f => f.At), sum, label);
    }
}
