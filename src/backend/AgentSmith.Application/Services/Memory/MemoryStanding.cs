using System.Globalization;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-10-25-9324d: one memory's track record — how often what relied on it was confirmed, strained or
/// contradicted, when it was last weighed, the decayed sum of its evidence and the label that sum earns.
/// </summary>
public sealed record MemoryStanding(
    string Slug, int Confirmed, int Strained, int Contradicted, DateTimeOffset? Last, double Sum, string Label)
{
    public static MemoryStanding None(string slug) => new(slug, 0, 0, 0, null, 0, MemoryStandingLabels.Unconfirmed);

    /// <summary>The record as a master reads it, e.g. 'confirmed 3 · strained 0 · contradicted 0 · last 2026-10-20'.</summary>
    public string Record => Last is not { } last
        ? "no record yet"
        : $"confirmed {Confirmed} · strained {Strained} · contradicted {Contradicted} · last "
          + last.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
