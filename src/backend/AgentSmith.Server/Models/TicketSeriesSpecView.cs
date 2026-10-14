using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59e: one spec of a published cut — its content from the cut, its standing from the
/// RunSpec rows, and each done criterion with the verdict of the attempt that last ended the spec.
/// </summary>
/// <param name="State">not_started | in_progress | done | failed | handed_back | carried.</param>
/// <param name="ExecutedBy">The attempt that executed the spec; null while nothing has.</param>
/// <param name="Unmeasured">Accounts that could not be taken for the spec, with why.</param>
/// <param name="Worked">2026-10-13-9a59f: each step as worked, in the spec's step order.</param>
/// <param name="FileStem">Null for a spec read from the run record: no cut named its file.</param>
/// <param name="HasRecord">False when the content is the goal alone — no spec record, or one that could not be read.</param>
/// <param name="RecordNote">Why only the goal is shown: <see cref="TicketSeriesRecordNotes"/>; null with a record.</param>
/// <param name="VerdictRecorded">False for a terminal spec none of whose criteria carries a verdict.</param>
public sealed record TicketSeriesSpecView(
    string SpecId,
    int Ordinal,
    string? FileStem,
    string Goal,
    IReadOnlyList<string> Repos,
    IReadOnlyList<SpecStep> Steps,
    string Yaml,
    string State,
    string? ExecutedBy,
    int? ExecutedByAttempt,
    IReadOnlyList<TicketSeriesCriterionView> Done,
    IReadOnlyList<UnmeasuredAccountView> Unmeasured,
    IReadOnlyList<TicketSeriesStepWorkedView>? Worked = null,
    bool HasRecord = true,
    string? RecordNote = null,
    bool VerdictRecorded = true);
