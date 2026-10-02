namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283di: one compare_reference call as the run keeps it — what was compared, each
/// viewport's two levels and the diff images written under the run record. A report, never a gate.
/// </summary>
public sealed record VisualComparison(
    string Reference, string Candidate, IReadOnlyList<ViewportComparison> Viewports, IReadOnlyList<string> Images);
