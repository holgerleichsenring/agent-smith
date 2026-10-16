using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// The verdicts of run-record specs. A spec reads its own row's acceptance, else the run-end acceptance
/// of the run that executed it: a spec with a done list joins it by criterion key; a spec without one
/// takes every criterion only when the acceptance is wholly its own (its row's, or a run with no other
/// spec). Run criteria no spec takes, and the unmeasured accounts of a run with several specs, are listed
/// per run as unattributed — never guessed onto a spec. A terminal spec none of whose criteria carries a
/// verdict says no verdict was recorded.
/// </summary>
public sealed class TicketRunRecordVerdicts
{
    private static readonly string[] Terminal = ["done", "failed", "handed_back"];

    public TicketRunRecordComposition Join(IReadOnlyList<TicketRunRecordSpec> specs, TicketRunRecord record)
    {
        var sources = specs.ToDictionary(s => s.SpecId, s => Source(s, record), StringComparer.Ordinal);
        var views = specs.Select(s => View(s, sources[s.SpecId], record)).ToList();
        return new TicketRunRecordComposition(views, Unattributed(views, sources, record));
    }

    private static TicketRunRecordAcceptance? Source(TicketRunRecordSpec spec, TicketRunRecord record)
    {
        if (spec.Executed is not { } row) return null;
        var single = record.Rows.Where(r => r.RunId == row.RunId).Select(r => r.SpecId).Distinct().Count() == 1;
        if (RunStoryJson.TryDeserialize<AcceptanceView>(row.AcceptanceJson) is { } own)
            return new TicketRunRecordAcceptance(row.RunId, own, OfTheRun: false, single);
        return RunStoryJson.TryDeserialize<AcceptanceView>(record.Runs[row.RunId].AcceptanceJson) is { } run
            ? new TicketRunRecordAcceptance(row.RunId, run, OfTheRun: true, single)
            : null;
    }

    private static TicketSeriesSpecView View(TicketRunRecordSpec spec, TicketRunRecordAcceptance? acceptance, TicketRunRecord record)
    {
        var judgements = JudgementsOf(acceptance, record);
        IReadOnlyList<TicketSeriesCriterionView> done = spec.Record is { } draft
            ? [.. draft.Done.Select(text => TicketSeriesCriterionJoin.Of(text, acceptance?.View, judgements))]
            : acceptance is { IsWhollyTheSpecs: true } ? TicketSeriesCriterionJoin.All(acceptance.View, judgements) : [];
        var unmeasured = acceptance is { IsWhollyTheSpecs: true } ? acceptance.View.Unmeasured : [];
        return new TicketSeriesSpecView(
            spec.SpecId, spec.Ordinal, null, spec.Record?.Goal ?? spec.Newest.Title, spec.Record?.Repos ?? [],
            spec.Record?.Steps ?? [], spec.RecordBody, spec.State,
            spec.Executed?.RunId, spec.Executed is null ? null : record.Numbers[spec.Executed.RunId],
            done, unmeasured, Worked: null,
            HasRecord: spec.Record is not null, RecordNote: spec.RecordNote,
            VerdictRecorded: !(Terminal.Contains(spec.State) && done.All(c => c.Verdict is null)), JudgedBy: acceptance?.RunId);
    }

    private static IReadOnlyList<TicketSeriesUnattributedView> Unattributed(
        IReadOnlyList<TicketSeriesSpecView> views, IReadOnlyDictionary<string, TicketRunRecordAcceptance?> sources,
        TicketRunRecord record) =>
        [.. sources.Values.OfType<TicketRunRecordAcceptance>().Where(a => a.OfTheRun)
            .GroupBy(a => a.RunId, StringComparer.Ordinal)
            .OrderBy(g => record.Numbers[g.Key])
            .Select(g => Leftover(g.First(), views.Where(v => sources[v.SpecId]?.RunId == g.Key), record))
            .OfType<TicketSeriesUnattributedView>()];

    private static TicketSeriesUnattributedView? Leftover(
        TicketRunRecordAcceptance acceptance, IEnumerable<TicketSeriesSpecView> takers, TicketRunRecord record)
    {
        var taken = takers.SelectMany(v => v.Done).Select(c => CriterionKey.Of(c.Text)).ToHashSet(StringComparer.Ordinal);
        var criteria = TicketSeriesCriterionJoin.All(acceptance.View, JudgementsOf(acceptance, record))
            .Where(c => !taken.Contains(CriterionKey.Of(c.Text))).ToList();
        var unmeasured = acceptance.SingleSpecRun ? [] : acceptance.View.Unmeasured;
        return criteria.Count == 0 && unmeasured.Count == 0
            ? null
            : new TicketSeriesUnattributedView(acceptance.RunId, record.Numbers[acceptance.RunId], criteria, unmeasured);
    }

    private static IReadOnlyList<RunCriterionJudgement> JudgementsOf(TicketRunRecordAcceptance? acceptance, TicketRunRecord record) =>
        acceptance is null ? [] : [.. record.Judgements.Where(j => j.RunId == acceptance.RunId)];
}
