using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59e: the series strip of every ticket on a list page, from one read of the page's
/// attempts. Cells come from the newest cut with specs and the RunSpec rows of its attempt; "asked" is
/// the newest attempt waiting on a checkpointed question while this spec is in progress in it. An
/// in-progress spec of an attempt that ended stopped there and reads failed; the attempt's own status
/// says why. The pull request beside the strip is the row's pullRequests, not a cell.
/// </summary>
public sealed class SeriesStripReader(TicketSeriesRecordsReader recordsReader)
{
    private const string InProgress = "in_progress";

    /// <param name="tickets">Each ticket's attempts, oldest first.</param>
    /// <returns>The strip by each ticket's newest attempt's run id.</returns>
    public async Task<IReadOnlyDictionary<string, TicketSeriesStripView>> ForAsync(
        IReadOnlyList<IReadOnlyList<Run>> tickets, CancellationToken ct)
    {
        var withAttempts = tickets.Where(t => t.Count > 0).ToList();
        var records = await recordsReader.ReadAsync([.. withAttempts.SelectMany(t => t.Select(r => r.Id))], full: false, ct);
        return withAttempts.ToDictionary(t => t[^1].Id, t => Strip(t, records), StringComparer.Ordinal);
    }

    private static TicketSeriesStripView Strip(IReadOnlyList<Run> attempts, TicketSeriesRecords records)
    {
        var ids = attempts.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        var cuts = TicketSeriesCuts.Ordered(records.Cuts.Where(c => ids.Contains(c.RunId)));
        var cut = TicketSeriesCuts.LatestWithSpecs(cuts);
        var newest = attempts[^1];
        var derive = IsLive(newest.Status) && cuts.All(c => c.RunId != newest.Id) ? "deriving"
            : TicketSeriesCuts.CurrentHandback(cuts) is not null ? "handed_back"
            : cut is not null ? "cut" : "none";
        if (cut is null) return new TicketSeriesStripView(derive, []);
        var cutRun = attempts.First(a => a.Id == cut.RunId);
        var carried = TicketSeriesCuts.Carried(records, cut.RunId);
        return new TicketSeriesStripView(derive,
            [.. TicketSeriesCuts.Entries(cut).Select(e => new TicketSeriesCellView(e.SpecId, Cell(e, cutRun, newest, carried, records)))]);
    }

    private static string Cell(SpecCutEntry spec, Run cutRun, Run newest, IReadOnlyList<string> carried, TicketSeriesRecords records)
    {
        if (carried.Contains(spec.SpecId, StringComparer.Ordinal)) return "carried";
        if (newest.Status == RunStatuses.WaitingForInput && records.Checkpointed.Contains(newest.Id)
            && Status(records, newest.Id, spec.SpecId) == InProgress)
            return "asked";
        return Status(records, cutRun.Id, spec.SpecId) switch
        {
            "done" => "done",
            "failed" => "failed",
            "handed_back" => "handed_back",
            InProgress => IsLive(cutRun.Status) ? "live" : "failed",
            _ => "not_started",
        };
    }

    private static string? Status(TicketSeriesRecords records, string runId, string specId) =>
        records.Specs.LastOrDefault(s => s.RunId == runId && s.SpecId == specId)?.Status;

    private static bool IsLive(string status) => status is "running" || RunStatuses.IsWaiting(status);
}
