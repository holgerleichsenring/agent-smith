namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59e: a ticket's series as its attempts published it — the latest cut with specs (or,
/// asked for one attempt, that attempt's cut), each spec with its content, standing and verdicts, the
/// gate the cut passed, and every revision the attempts worked from.
/// </summary>
/// <param name="RunId">The attempt the cut came from; null when no attempt published a cut.</param>
/// <param name="Attempt">That attempt's number, from 1 by run id.</param>
/// <param name="Handback">A hand-back as the current state: newer than the cut with specs, or the
/// asked attempt's own case.</param>
/// <param name="Recorded">False when no attempt published a cut and no attempt recorded a spec row.</param>
/// <param name="Source">cut, or run-record when no cut is in scope and the specs come from the attempts'
/// spec rows, spec records and acceptance (<see cref="TicketSeriesSources"/>).</param>
/// <param name="UnattributedCriteria">Run acceptance a run-record read could not tie to one spec, per run.</param>
public sealed record TicketSeriesView(
    string Project,
    string TicketId,
    string? RunId,
    int? Attempt,
    DateTimeOffset? At,
    string? SetKey,
    TicketSeriesHandbackView? Handback,
    TicketSeriesGateView? Gate,
    IReadOnlyList<TicketSeriesSpecView> Specs,
    IReadOnlyList<TicketSeriesRevisionView> Revisions,
    bool Recorded,
    string Source = TicketSeriesSources.Cut,
    IReadOnlyList<TicketSeriesUnattributedView>? UnattributedCriteria = null);
