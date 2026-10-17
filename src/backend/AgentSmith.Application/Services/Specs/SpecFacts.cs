using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: one spec's resolved fact lines — what cites minted evidence and
/// what does not — as the renderer writes them and the reader reads them back.
/// </summary>
/// <param name="Unsettled">2026-10-17-293bc: claims whose cited looks could not run, each with
/// the line that says so. Rendered for a reader, never read back as a premise.</param>
public sealed record SpecFacts(
    IReadOnlyList<SpecFact> Facts,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<SpecFact>? Unsettled = null)
{
    public static readonly SpecFacts None = new([], []);

    public IReadOnlyList<SpecFact> Unsettled { get; init; } = Unsettled ?? [];

    public bool IsEmpty => Facts.Count == 0 && Assumptions.Count == 0 && Unsettled.Count == 0;
}
