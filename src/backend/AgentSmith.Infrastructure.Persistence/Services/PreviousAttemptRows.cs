using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// The ticket's code runs that count as attempts for <see cref="DbPreviousAttemptReader"/>: no queued reservation, and
/// (2026-11-19-1b72a) no start that never began — a reservation the capacity pump dropped on a refusal (cancelled, no
/// cancel reason, no step), whatever refused it, so the next start is still the ticket's first. A queued run a person
/// cancelled carries its reason and stays an attempt, so the poll does not pick a cancelled ticket up again.
/// </summary>
internal static class PreviousAttemptRows
{
    private const string Cancelled = "cancelled"; // the status every cancel path finalizes a run with

    public static IQueryable<Run> Of(IQueryable<Run> runs, IQueryable<RunStep> steps, string project, string ticketId) =>
        Counted(runs.AsNoTracking().Where(r => r.Project == project && r.TicketId == ticketId), steps);

    /// <summary>2026-11-22-9719d: the same rows for several tickets of the project.</summary>
    public static IQueryable<Run> Of(IQueryable<Run> runs, IQueryable<RunStep> steps, string project, IReadOnlyCollection<string> ticketIds) =>
        Counted(runs.AsNoTracking().Where(r => r.Project == project && ticketIds.Contains(r.TicketId)), steps);

    private static IQueryable<Run> Counted(IQueryable<Run> runs, IQueryable<RunStep> steps) =>
        runs.Where(r => r.Pipeline == PipelinePresets.CodeName && r.Status != RunStatuses.Queued
            && !(r.Status == Cancelled && r.CancelReason == null && !steps.Any(s => s.RunId == r.Id)));
}
