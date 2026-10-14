using AgentSmith.Contracts.Commands;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59e: a ticket's series as its attempts PUBLISHED it — the cut rows, never a git read.
/// By default the latest cut with specs across the attempts, with a newer hand-back reported as the
/// current state above it; asked for one attempt, that attempt's cut, its hand-back case and its gate.
/// A reviewer's edit on the branch is not here until an attempt publishes it. With no cut at all in
/// scope, the series is what the attempts recorded instead (<see cref="TicketRunRecordSeriesReader"/>).
/// </summary>
public sealed class TicketSeriesReader(
    TicketRunsQuery runQuery,
    TicketAttempts attempts,
    TicketSeriesRecordsReader recordsReader,
    TicketSeriesSpecComposer specs,
    TicketRunRecordSeriesReader runRecords,
    Runway.SeriesRunwayReader runway)
{
    private const string Success = "success";

    /// <summary>Null when the pair is not a ticket, or <paramref name="runId"/> is not one of its attempts.</summary>
    public async Task<TicketSeriesView?> ReadAsync(string project, string ticketId, string? runId, CancellationToken ct)
    {
        var code = (await runQuery.ForTicketAsync(project, ticketId, ct))
            .Where(r => r.Run.Pipeline == PipelinePresets.CodeName).ToList();
        if (code.Count == 0) return null;
        var numbers = attempts.Of(code).Select((a, i) => (a.Run.Id, Number: i + 1))
            .ToDictionary(a => a.Id, a => a.Number, StringComparer.Ordinal);
        if (runId is not null && !numbers.ContainsKey(runId)) return null;

        var records = await recordsReader.ReadAsync([.. numbers.Keys], full: true, ct);
        var all = TicketSeriesCuts.Ordered(records.Cuts);
        var scoped = runId is null ? all : [.. all.Where(c => c.RunId == runId)];
        var cut = TicketSeriesCuts.LatestWithSpecs(scoped) ?? (runId is null ? null : scoped.LastOrDefault());
        var handback = runId is null ? TicketSeriesCuts.CurrentHandback(all) : scoped.LastOrDefault(c => c.HandbackCase is not null);
        var revisions = Revisions(records, all, numbers);
        if (cut is null)
            return scoped.Count == 0
                   && await runRecords.ReadAsync(records, [.. code.Select(r => r.Run)], numbers, runId, ct) is { } fromRuns
                ? new TicketSeriesView(project, ticketId, runId, runId is null ? null : numbers[runId], null, null,
                    Handback(handback, numbers), null, fromRuns.Specs, revisions, Recorded: true,
                    TicketSeriesSources.RunRecord, fromRuns.Unattributed)
                : new TicketSeriesView(project, ticketId, null, null, null, null,
                    Handback(handback, numbers), null, [], revisions, all.Count > 0);

        var carried = TicketSeriesCuts.Carried(records, cut.RunId);
        var entries = TicketSeriesCuts.Entries(cut);
        return new TicketSeriesView(
            project, ticketId, cut.RunId, numbers[cut.RunId], cut.At, cut.SetKey,
            Handback(handback, numbers), Gate(cut, entries.Count, records),
            [.. entries.Select(e => specs.Compose(e, cut.RunId, carried, records, numbers))],
            revisions, Recorded: true, Budget: runId is null ? (await runway.ReadAsync(project, ticketId, ct))?.Page : null);
    }

    private static TicketSeriesHandbackView? Handback(RunSeriesCut? cut, IReadOnlyDictionary<string, int> numbers) =>
        cut?.HandbackCase is { } handbackCase
            ? new TicketSeriesHandbackView(handbackCase, cut.RunId, numbers[cut.RunId], cut.At)
            : null;

    // "skipped" is read from the cut's case, not the step: the gate answers OK for a handed-back set — but a review objection's prefix was gated (8762l).
    private static TicketSeriesGateView? Gate(RunSeriesCut cut, int specCount, TicketSeriesRecords records)
    {
        if (cut.HandbackCase is { } handed && handed != Application.Services.Specs.SpecCutContent.ReviewObjection) return new("skipped", specCount, cut.RunId);
        return records.GateSteps.GetValueOrDefault(cut.RunId) is { } status
            ? new(status == Success ? "passed" : "failed", specCount, cut.RunId)
            : null;
    }

    private static IReadOnlyList<TicketSeriesRevisionView> Revisions(
        TicketSeriesRecords records, IReadOnlyList<RunSeriesCut> cuts, IReadOnlyDictionary<string, int> numbers) =>
        [.. numbers.OrderBy(n => n.Value)
            .Select(n => (n.Key, n.Value, Row: records.Attempts.GetValueOrDefault(n.Key)))
            .Where(a => a.Row?.RevisionNumber is not null)
            .Select(a => new TicketSeriesRevisionView(a.Row!.RevisionNumber!.Value, a.Row.RevisionCause, a.Key, a.Value,
                cuts.LastOrDefault(c => c.RunId == a.Key)?.At))];
}
