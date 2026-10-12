using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: one spec's resolved fact lines — what cites minted evidence and
/// what does not — as the renderer writes them and the reader reads them back.
/// </summary>
public sealed record SpecFacts(
    IReadOnlyList<SpecFact> Facts,
    IReadOnlyList<string> Assumptions)
{
    public static readonly SpecFacts None = new([], []);

    public bool IsEmpty => Facts.Count == 0 && Assumptions.Count == 0;
}
