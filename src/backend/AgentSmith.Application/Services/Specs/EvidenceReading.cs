namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-02-3f06b: one evidence line read into what a check can act on — the paths it cites,
/// the observation segments it states, and the minted look lines it carries.
/// </summary>
public sealed record EvidenceReading(
    IReadOnlyList<EvidenceReference> References,
    IReadOnlyList<string> Observations,
    IReadOnlyList<string> Minted)
{
    public bool IsEmpty => References.Count == 0 && Observations.Count == 0 && Minted.Count == 0;
}
