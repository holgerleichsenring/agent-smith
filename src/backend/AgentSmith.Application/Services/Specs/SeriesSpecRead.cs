using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>2026-10-06-03c7d: one spec of a series read back, or why it could not be.</summary>
/// <param name="Executed">2026-10-06-03c7e: the spec lay in <c>specs/done/</c> — it ran.</param>
public sealed record SeriesSpecRead(SpecPhase? Phase, string? Why, bool Executed = false)
{
    public static SeriesSpecRead Read(SpecPhase phase, bool executed = false) => new(phase, null, executed);

    public static SeriesSpecRead Failed(string why) => new(null, why);
}
