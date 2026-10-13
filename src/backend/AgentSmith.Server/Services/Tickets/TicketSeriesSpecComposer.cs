using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59e: one spec of a cut as the ticket reads it. Content comes from the cut; the standing
/// from the cut attempt's RunSpec row, or "carried" when that attempt carried the spec in executed; the
/// attempt that executed it is the newest one whose row left not_started; each done criterion takes its
/// verdict from the row of the newest attempt that ENDED the spec, matched by criterion text, and that
/// run's operator judgement of it.
/// </summary>
public sealed class TicketSeriesSpecComposer
{
    private const string NotStarted = "not_started";
    private const string Carried = "carried";

    public TicketSeriesSpecView Compose(
        SpecCutEntry spec, string cutRunId, IReadOnlyList<string> carried,
        TicketSeriesRecords records, IReadOnlyDictionary<string, int> attemptNumbers)
    {
        var rows = records.Specs.Where(r => r.SpecId == spec.SpecId && attemptNumbers.ContainsKey(r.RunId))
            .OrderBy(r => r.RunId, StringComparer.Ordinal).ToList();
        var state = carried.Contains(spec.SpecId, StringComparer.Ordinal) ? Carried
            : rows.LastOrDefault(r => r.RunId == cutRunId)?.Status ?? NotStarted;
        var executed = rows.LastOrDefault(r => r.Status != NotStarted);
        var ended = rows.LastOrDefault(r => r.EndedAt is not null && r.AcceptanceJson is not null);
        var acceptance = RunStoryJson.TryDeserialize<AcceptanceView>(ended?.AcceptanceJson);
        var judgements = ended is null ? [] : records.Judgements.Where(j => j.RunId == ended.RunId).ToList();
        return new TicketSeriesSpecView(
            spec.SpecId, spec.Ordinal, spec.FileStem, spec.Goal, spec.Repos, spec.Steps, spec.Yaml, state,
            executed?.RunId, executed is null ? null : attemptNumbers[executed.RunId],
            [.. spec.Done.Select(text => Criterion(text, acceptance, judgements))],
            acceptance?.Unmeasured ?? []);
    }

    private static TicketSeriesCriterionView Criterion(
        string text, AcceptanceView? acceptance, List<RunCriterionJudgement> judgements)
    {
        var key = CriterionKey.Of(text);
        var row = acceptance?.Criteria.FirstOrDefault(c => CriterionKey.Of(c.Text) == key);
        var declined = acceptance?.Declined?.FirstOrDefault(d => CriterionKey.Of(d.Text) == key);
        var judged = judgements.LastOrDefault(j => j.CriterionKey == key);
        return new TicketSeriesCriterionView(
            text,
            row?.Status ?? (declined is null ? null : AcceptanceCriterionStatuses.NotApplicable),
            row?.Reason ?? declined?.Reason,
            row?.Citation,
            judged is null ? null : new TicketCriterionOverrule(judged.HumanStatus, judged.Reason, judged.Author, judged.RecordedAt));
    }
}
