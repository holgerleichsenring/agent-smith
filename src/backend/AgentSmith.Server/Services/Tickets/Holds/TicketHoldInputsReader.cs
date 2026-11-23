using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Preconditions;
using AgentSmith.Server.Services.Series;
using AgentSmith.Server.Services.Tickets.Text;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets.Holds;

/// <summary>
/// 2026-11-23-31dcg: the hold inputs of one read, in a fixed number of reads whatever the number of tickets — one series-run
/// read for the scope, and per project the series page's own set-based facts (<see cref="SeriesRunProjectReads"/>) and its
/// merge order (<see cref="SeriesRunTickets"/>), so a ticket's merge hold is the order the series page shows; one ledger
/// read for the open text edits. On a scope of its own: the readers that call it are transient.
/// <para>2026-11-23-849e: the series reads live in <see cref="TicketHoldSeriesRead"/>.</para>
/// <para>2026-11-23-31dci: and one read each of the open model waits and the capacity queue's arrivals, both bounded by
/// what runs or waits now, with <c>holds.escalate_after_minutes</c> and the clock the waits are aged by.</para>
/// </summary>
public sealed class TicketHoldInputsReader(
    IServiceScopeFactory scopes, InterruptedRestartWindow? restart = null, TimeProvider? clock = null)
{
    /// <param name="holds">2026-11-23-31dci: the holds block; null reports no wait to the admin.</param>
    /// <param name="ticketId">One ticket's inputs (the ticket page); null for every ticket of the scope.</param>
    public async Task<TicketHoldInputs> ReadAsync(
        ProjectScope scope, IReadOnlyDictionary<string, ResolvedProject> configured, PreconditionsConfig? preconditions,
        HoldsConfig? holds, string? ticketId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(configured);
        using var services = scopes.CreateScope();
        var series = await TicketHoldSeriesRead.ReadAsync(services.ServiceProvider, scope, configured, ct);
        var edited = await EditedAsync(services.ServiceProvider, scope, ticketId, ct);
        var since = preconditions is null || restart is null ? null : restart.Since(preconditions);
        var waits = await services.ServiceProvider.GetRequiredService<ModelRunWaitClock>().WaitingSinceAsync(ct);
        var enqueued = await services.ServiceProvider.GetRequiredService<QueuedTicketRepository>().EnqueuedAtAsync(ct);
        return new TicketHoldInputs(series.Series, series.Steps, edited, since, series.Active)
        {
            Plans = series.Plans, Approving = Approving(configured), // 2026-11-23-849e
            ModelWaits = waits, Enqueued = enqueued, EscalateAfterMinutes = holds?.EscalateAfterMinutes ?? 0,
            Now = (clock ?? TimeProvider.System).GetUtcNow(),
        };
    }

    // 2026-11-23-849e: a merge run approves where the host requires a review unless the project bypasses the protection.
    private static HashSet<string> Approving(IReadOnlyDictionary<string, ResolvedProject> configured) =>
        [.. configured.Values.Where(p => p.Merge.Protection == BranchProtection.Respect).Select(p => p.Name)];

    private static async Task<HashSet<(string, string)>> EditedAsync(
        IServiceProvider services, ProjectScope scope, string? ticketId, CancellationToken ct)
    {
        var rows = await services.GetRequiredService<ReworkLedgerRepository>()
            .WithKeyPrefixAsync(TicketTextEditNotice.KeyPrefix, scope.Only, ticketId, ct);
        return [.. rows.Where(r => scope.Admits(r.Project) && TicketTextEditNotice.Open(r.Keys).Count > 0).Select(r => (r.Project, r.TicketId))];
    }
}
