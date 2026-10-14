using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// The series of attempts that published no cut, read from what they recorded instead — nothing is
/// written or backfilled. The default read covers every attempt; asked for one, only that attempt's rows.
/// </summary>
public sealed class TicketRunRecordSeriesReader(TicketSpecRecordsReader specRecords, TicketRunRecordSpecComposer composer)
{
    /// <returns>Null when the attempts in scope recorded no spec row.</returns>
    public async Task<TicketRunRecordComposition?> ReadAsync(
        TicketSeriesRecords records, IReadOnlyList<Run> runs, IReadOnlyDictionary<string, int> numbers,
        string? runId, CancellationToken ct)
    {
        var scope = numbers.Where(n => runId is null || n.Key == runId)
            .ToDictionary(n => n.Key, n => n.Value, StringComparer.Ordinal);
        var rows = records.Specs.Where(r => scope.ContainsKey(r.RunId)).ToList();
        if (rows.Count == 0) return null;
        var stored = await specRecords.ReadAsync([.. scope.Keys], ct);
        return composer.Compose(new TicketRunRecord(
            rows, stored, runs.ToDictionary(r => r.Id, StringComparer.Ordinal), records.Judgements, scope));
    }
}
