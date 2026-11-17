using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59e: one spec of a cut as the ticket reads it. The place comes from the cut; the standing
/// from the cut attempt's RunSpec row, or "carried" when that attempt carried the spec in executed; the
/// attempt that executed it is the newest one whose row left not_started; each done criterion takes its
/// verdict from the row of the newest attempt that ENDED the spec, matched by criterion text, and that
/// run's operator judgement of it. 2026-10-13-8762m: and the build note of that attempt's record.
/// </summary>
/// <para>
/// 2026-11-17-a0b6b: the text — goal, steps, done and yaml — is the branch's spec of the entry's id; the cut
/// gives only the id, place and stem. A spec the branch no longer holds shows its id, stem and the cut's goal.
/// </para>
public sealed class TicketSeriesSpecComposer
{
    public TicketSeriesSpecView Compose(
        SpecCutEntry spec, DerivedSpec? text, string cutRunId, IReadOnlyList<string> carried,
        TicketSeriesRecords records, IReadOnlyDictionary<string, int> attemptNumbers)
    {
        var rows = records.Specs.Where(r => r.SpecId == spec.SpecId && attemptNumbers.ContainsKey(r.RunId))
            .OrderBy(r => r.RunId, StringComparer.Ordinal).ToList();
        var state = TicketSeriesCuts.StateOf(records, cutRunId, carried, spec.SpecId);
        var executed = rows.LastOrDefault(r => r.Status != TicketSeriesCuts.NotStarted);
        var ended = rows.LastOrDefault(r => r.EndedAt is not null && r.AcceptanceJson is not null);
        var acceptance = RunStoryJson.TryDeserialize<AcceptanceView>(ended?.AcceptanceJson);
        var judgements = ended is null ? [] : records.Judgements.Where(j => j.RunId == ended.RunId).ToList();
        var draft = text?.Draft;
        var steps = draft?.Steps ?? [];
        return new TicketSeriesSpecView(
            spec.SpecId, spec.Ordinal, text?.FileStem ?? spec.FileStem, draft?.Goal ?? spec.Goal, draft?.Repos ?? spec.Repos,
            steps, draft?.Yaml ?? string.Empty, state,
            executed?.RunId, executed is null ? null : attemptNumbers[executed.RunId],
            [.. (draft?.Done ?? []).Select(done => TicketSeriesCriterionJoin.Of(done, acceptance, judgements))],
            acceptance?.Unmeasured ?? [],
            StepsAsWorkedFold.Fold(spec.SpecId, steps, records.Transitions ?? [], attemptNumbers), // 2026-10-13-9a59f
            BuildNote(spec.SpecId, executed?.RunId, records), JudgedBy: ended?.RunId); // 2026-10-16-86e6e
    }

    // 2026-10-13-8762m: the note lives in the executing attempt's record, its only home.
    private static TicketSeriesBuildNoteView? BuildNote(string specId, string? runId, TicketSeriesRecords records) =>
        runId is not null && records.Records?.TryGetValue((runId, specId), out var body) == true
            ? TicketSeriesBuildNoteView.Of(Application.Services.Specs.SpecBuildNoteReader.Read(body, specId))
            : null;
}
