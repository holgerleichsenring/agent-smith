using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0393a: one phase of the derived set — the schema-valid spec plus the markdown
/// companion that carries the ticket spans this phase is responsible for. Two
/// files, because a phase spec states WHAT and the manual's code templates are
/// what a summary destroys.
/// </summary>
public sealed record SpecPhase(
    PhaseDraft Draft,
    string Slug,
    string Markdown,
    IReadOnlyList<int> CarriedSegments)
{
    /// <summary>Shared stem of the yaml and its markdown companion.</summary>
    public string FileStem => $"{Draft.PhaseId}-{Slug}";

    public string PhaseId => Draft.PhaseId;
}
