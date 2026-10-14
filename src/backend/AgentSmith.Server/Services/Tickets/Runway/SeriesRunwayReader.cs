using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets.Runway;

/// <summary>
/// 2026-10-13-8762v: composes the runway's facts once — the ticket's code runs, the series rows of its attempts,
/// the holding defaults, the operator-stop watermark, the worker's refusal and close marks and the LIVE series
/// settings — so the rework worker and the ticket page answer from one function.
/// </summary>
public sealed class SeriesRunwayReader(
    TicketRunsQuery runQuery,
    TicketAttempts attempts,
    TicketSeriesRecordsReader recordsReader,
    SeriesHolds holds,
    ISeriesSettingsReader settings,
    IServiceScopeFactory scopes,
    TimeProvider clock)
{
    private const string Done = "done";

    /// <summary>The keys the worker marks under the newest run.</summary>
    public static string RefusedKey(string newest) => $"{newest}|series-refused";

    public static string ClosedKey(string newest) => $"{newest}|series-closed";

    public static string HeldKey(string newest, string code) => $"{newest}|series-held|{code}";

    /// <summary>Null when the ticket has no code run.</summary>
    public async Task<SeriesRunwayRead?> ReadAsync(string project, string ticketId, CancellationToken ct)
    {
        var code = (await runQuery.ForTicketAsync(project, ticketId, ct))
            .Where(r => r.Run.Pipeline == PipelinePresets.CodeName).ToList();
        if (code.Count == 0) return null;
        var tried = attempts.Of(code).Select(r => r.Run).ToList();
        var newest = code.Where(r => r.Run.Status != RunStatuses.Queued).MaxBy(r => r.Run.Id, StringComparer.Ordinal)?.Run;
        var records = await recordsReader.ReadAsync([.. tried.Select(r => r.Id)], full: false, ct);
        var cut = TicketSeriesCuts.LatestWithSpecs(TicketSeriesCuts.Ordered(records.Cuts));
        var entries = cut is null ? [] : TicketSeriesCuts.Entries(cut);
        var finished = cut is null ? [] : records.Specs
            .Where(s => s.Status == Done && string.CompareOrdinal(s.RunId, cut.RunId) >= 0).Select(s => s.SpecId)
            .Concat(TicketSeriesCuts.Carried(records, cut.RunId)).ToHashSet(StringComparer.Ordinal);
        var left = entries.Count(e => !finished.Contains(e.SpecId));
        var (watermark, refused, closed) = await MarksAsync(project, ticketId, newest?.Id, ct);
        var current = settings.Current();
        var facts = new SeriesRunwayFacts(tried,
            records.Attempts.ToDictionary(a => a.Key, a => a.Value.CauseChannel, StringComparer.Ordinal),
            newest, code.Any(r => r.Run.Status == RunStatuses.Queued), cut is not null, left,
            newest is not null && records.Specs.Any(s => s.RunId == newest.Id && s.Status == Done),
            await holds.PendingAsync(project, ticketId, ct), watermark, refused, closed, current);
        return new SeriesRunwayRead(SeriesRunway.Judge(facts, clock.GetUtcNow()), newest?.Id, left, entries.Count,
            SeriesRunway.Chain(facts).Count, current);
    }

    private async Task<(DateTimeOffset? Watermark, bool Refused, bool Closed)> MarksAsync(
        string project, string ticketId, string? newest, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var ledger = scope.ServiceProvider.GetRequiredService<ReworkLedgerRepository>();
        var watermark = await ledger.WatermarkAsync(project, ticketId, ct);
        if (newest is null) return (watermark, false, false);
        return (watermark, await ledger.IsSpokenAsync(project, ticketId, RefusedKey(newest), ct),
            await ledger.IsSpokenAsync(project, ticketId, ClosedKey(newest), ct));
    }
}
