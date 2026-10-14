using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// The specs of attempts that published no cut, from what they did record: one spec per distinct spec id
/// of their RunSpec rows, ordered by its first attempt then its ordinal there and numbered by that order.
/// Its state is the newest attempt's row — in_progress on a run that is not live reads failed, as the
/// strip reads it — and its content the newest spec record, read tolerantly; without a readable record
/// only the goal the row kept is shown, and the spec says why. Verdicts are joined by
/// <see cref="TicketRunRecordVerdicts"/>.
/// </summary>
public sealed class TicketRunRecordSpecComposer(SpecRecordReader recordReader, TicketRunRecordVerdicts verdicts)
{
    private const string NotStarted = "not_started";
    private const string InProgress = "in_progress";
    private const string Failed = "failed";
    private const string Running = "running";

    public TicketRunRecordComposition Compose(TicketRunRecord record)
    {
        var specs = record.Rows.Where(r => record.Numbers.ContainsKey(r.RunId))
            .GroupBy(r => r.SpecId, StringComparer.Ordinal)
            .Select(g => g.OrderBy(r => record.Numbers[r.RunId]).ToList())
            .OrderBy(rows => record.Numbers[rows[0].RunId]).ThenBy(rows => rows[0].Ordinal)
            .Select((rows, i) => Spec(rows, i + 1, record))
            .ToList();
        return verdicts.Join(specs, record);
    }

    private TicketRunRecordSpec Spec(List<RunSpec> rows, int ordinal, TicketRunRecord record)
    {
        var newest = rows[^1];
        var kind = RunSpecProjection.RecordKindPrefix + newest.SpecId;
        var body = record.SpecRecords.Where(a => a.Kind == kind && record.Numbers.ContainsKey(a.RunId))
            .MaxBy(a => record.Numbers[a.RunId])?.Content;
        var parsed = body is null ? null : recordReader.Read(newest.SpecId, body);
        var note = body is null ? TicketSeriesRecordNotes.NoRecord
            : parsed is null ? TicketSeriesRecordNotes.Unreadable : null;
        return new TicketRunRecordSpec(
            newest.SpecId, ordinal, State(newest, record.Runs), newest,
            rows.LastOrDefault(r => r.Status != NotStarted), parsed, parsed is null ? string.Empty : body!, note);
    }

    private static string State(RunSpec newest, IReadOnlyDictionary<string, Run> runs) =>
        newest.Status == InProgress && !IsLive(runs[newest.RunId].Status) ? Failed : newest.Status;

    private static bool IsLive(string status) => status is Running || RunStatuses.IsWaiting(status);
}
