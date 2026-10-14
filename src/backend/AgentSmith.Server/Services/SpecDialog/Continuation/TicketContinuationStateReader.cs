using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-9a59l: reads what <see cref="TicketContinuation.Decide"/> decides over. WORKED is a code
/// attempt in ANY project sharing the tracker — one record serves them all, so a sibling's run read as
/// never-worked would write a fresh branch set over the one that run published; a ticket only a
/// pr-review or scan run touched is never-worked. The basis is the latest cut with specs its attempts
/// published, with the pointer's executed ids, else the approval record's set. A ticket the tracker
/// cannot answer for is not guessed about: the read fails with the reason.
/// </summary>
public sealed class TicketContinuationStateReader(
    AgentSmithConfig config, FiledWorkTrackerProjects trackerProjects, ITicketProviderFactory trackers,
    TicketClosure closure, TicketRunsQuery runs, TicketAttempts attempts, TicketSeriesRecordsReader seriesRecords,
    ISpecSetPointerStore pointers, ApprovedSpecSetRecorder approvals, OperatorStartArm startArm,
    ILogger<TicketContinuationStateReader> logger)
{
    /// <param name="mayStartRuns">2026-10-13-9a59m: whether the approver holds runs.control — the start arm's input.</param>
    public async Task<TicketContinuationRead> ReadAsync(string projectName, string ticketId, bool mayStartRuns, CancellationToken ct)
    {
        if (!config.Projects.TryGetValue(projectName, out var project))
            return TicketContinuationRead.Failed($"Project '{projectName}' is no longer in the configuration catalog.");
        bool closed;
        try
        {
            var provider = trackers.Create(project.Tracker);
            closed = await closure.ClosedForWorkAsync(project, provider, await provider.GetTicketAsync(new TicketId(ticketId), ct), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Ticket {Ticket} of project {Project} could not be read for an approval", ticketId, projectName);
            return TicketContinuationRead.Failed($"Ticket {ticketId} could not be read from its tracker ({ex.Message}), so "
                + "whether it is open, closed or worked is unknown. Nothing was recorded; approve again once the tracker answers.");
        }

        var rows = new List<TicketRunRow>();
        foreach (var sibling in trackerProjects.SharingTrackerWith(projectName))
            rows.AddRange(await runs.ForTicketAsync(sibling.Name, ticketId, ct));
        var worked = attempts.Of(rows).OrderBy(r => r.Run.Id, StringComparer.Ordinal).ToList();
        var record = await approvals.LoadAsync(project, ticketId, ct);
        var records = await seriesRecords.ReadAsync([.. worked.Select(r => r.Run.Id)], full: false, ct);
        var cut = TicketSeriesCuts.LatestWithSpecs(TicketSeriesCuts.Ordered(records.Cuts));
        var basisRun = worked.LastOrDefault();
        var pointer = basisRun is null ? null : await pointers.GetAsync(basisRun.Run.Project,
            TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value, ct);
        var carried = cut is null ? [] : TicketSeriesCuts.Carried(records, cut.RunId);
        IReadOnlyList<BasisSpec> basis = cut is not null
            ? [.. TicketSeriesCuts.Entries(cut).Select(e => new BasisSpec(e.SpecId, e.Goal, e.Yaml,
                TicketSeriesCuts.StateOf(records, cut.RunId, carried, e.SpecId)))]
            : [.. record?.Set.Specs.Select(s => new BasisSpec(s.SpecId, s.Draft.Goal, s.Draft.Yaml)) ?? []];
        var executed = pointer is null ? []
            : pointer.ExecutedIds is { Count: > 0 } ids ? ids : [.. basis.Take(pointer.ExecutedThrough).Select(b => b.SpecId)];
        var series = (basisRun is null ? null : records.Attempts.GetValueOrDefault(basisRun.Run.Id)?.SeriesId)
            ?? pointer?.SeriesId ?? record?.Set.Series;
        var scope = new ContinuationBasis(
            pointer is not null ? [.. pointer.ShaByRepo.Keys] : record?.Repositories ?? [],
            NonEmpty(pointer?.CarryingRepo) ?? NonEmpty(record?.CarryingRepo)) { Shas = pointer?.ShaByRepo };
        var start = await startArm.ReadAsync(projectName, ticketId, mayStartRuns, ct);
        return new(new TicketContinuationState(ticketId, closed, basisRun?.Run.Id, series, basis, executed, scope, record, start), null);
    }

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
