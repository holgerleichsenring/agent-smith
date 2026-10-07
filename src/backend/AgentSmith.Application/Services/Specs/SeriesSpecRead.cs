using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>2026-10-06-03c7d: one spec of a series read back, or why it could not be.</summary>
public sealed record SeriesSpecRead(SpecPhase? Phase, string? Why)
{
    public static SeriesSpecRead Read(SpecPhase phase) => new(phase, null);

    public static SeriesSpecRead Failed(string why) => new(null, why);
}
