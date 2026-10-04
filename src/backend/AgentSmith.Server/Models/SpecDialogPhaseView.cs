using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-15-6d9c: one drafted phase as the proposal pane renders it — the fields the
/// design master's own phase template emits (goal / steps / tests / done), plus the
/// requires: edges that decide where a child sits in the order an epic is filed.
/// </summary>
/// <param name="Steps">The step ACTIONS, in spec order; the pane has no use for a step id.</param>
/// <param name="Yaml">The spec as the master wrote it — the raw form the pane can disclose,
/// since the reply shown beside it on the dashboard no longer carries it.</param>
public sealed record SpecDialogPhaseView(
    string PhaseId,
    string Goal,
    IReadOnlyList<string> Steps,
    IReadOnlyList<string> Tests,
    IReadOnlyList<string> Done,
    IReadOnlyList<string> Requires,
    string Yaml)
{
    /// <summary>2026-10-02-3f06c: what the phase rests on — each fact with the evidence it cites.
    /// Not a constructor parameter, so the dashboard type gains an optional field.</summary>
    public IReadOnlyList<PhaseFact> Facts { get; init; } = [];

    /// <summary>2026-10-02-3f06c: what the phase states without having looked.</summary>
    public IReadOnlyList<string> Assumptions { get; init; } = [];
}
