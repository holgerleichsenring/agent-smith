using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0466: one phase of a run, as the operator opens it — what it was asked to do, where
/// it ended up, the decisions taken inside it and the steps it ran.
/// <para>
/// Everything here is read from rows the PRODUCER wrote: the phase row states its own
/// ordinal, title and standing, and the steps and decisions name their phase in a column.
/// Nothing on this path derives a phase by parsing a display name.
/// </para>
/// </summary>
public sealed record RunPhaseView(
    string PhaseId,
    int Ordinal,
    string Title,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    string? Verdict,
    IReadOnlyList<RunDecisionView> Decisions,
    IReadOnlyList<RunStepView> Steps);
