using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: one phase's resolved fact lines — what cites minted evidence and
/// what does not — as the renderer writes them and the reader reads them back.
/// </summary>
public sealed record PhaseFacts(
    IReadOnlyList<PhaseFact> Facts,
    IReadOnlyList<string> Assumptions)
{
    public static readonly PhaseFacts None = new([], []);

    public bool IsEmpty => Facts.Count == 0 && Assumptions.Count == 0;
}
