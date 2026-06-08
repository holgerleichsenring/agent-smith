using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0246d: makes the DB the system-of-record for ticket lifecycle. A transition
/// writes the authoritative DB status FIRST, then writes the platform label as a
/// BEST-EFFORT projection through the inner transitioner — a label that fails to
/// update is logged, not fatal, and never authoritative. ReadCurrent returns the
/// DB status, falling back to the label only when the DB has no row yet. On drift
/// the DB wins.
/// </summary>
public sealed class DbAuthoritativeTicketStatusTransitioner(
    ITicketStatusTransitioner inner,
    IServiceScopeFactory scopeFactory,
    string project,
    string platform,
    ILogger logger) : ITicketStatusTransitioner
{
    public string ProviderType => inner.ProviderType;

    public async Task<TicketLifecycleStatus?> ReadCurrentAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        // Per-operation scope — a transitioner is not a web request.
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<TicketLifecycleRepository>();
        var dbStatus = await repo.GetStatusAsync(project, platform, ticketId, cancellationToken);
        if (dbStatus is not null && Enum.TryParse<TicketLifecycleStatus>(dbStatus, out var parsed))
            return parsed;
        return await inner.ReadCurrentAsync(ticketId, cancellationToken);
    }

    public async Task<TransitionResult> TransitionAsync(
        TicketId ticketId, TicketLifecycleStatus from, TicketLifecycleStatus to, CancellationToken cancellationToken)
    {
        // DB first — authoritative.
        using (var scope = scopeFactory.CreateScope())
            await scope.ServiceProvider.GetRequiredService<TicketLifecycleRepository>()
                .SetStatusAsync(project, platform, ticketId, to.ToString(), cancellationToken);

        // Label as a best-effort projection. A failure is logged, never fatal.
        try
        {
            // p0258: the label is a PROJECTION of the now-authoritative DB — move it
            // to `to` from wherever the label ACTUALLY is, not from the caller's
            // `from`. The run-end path (TicketAwarePipelineLifecycleCoordinator)
            // passes from=Pending when it can't read current; the label was
            // in-progress, so the strict from-precondition rejected the write and
            // the terminal tag (agent-smith:failed/done) never landed. The ticket
            // then fell back to a claimable [agent-smith:bug] with no lifecycle tag
            // and the poller auto-re-claimed it — the "inconsistent tag state /
            // job re-triggers itself" loop. Re-anchoring on the label's real state
            // makes the projection always land; the DB lease (p0246b) is the
            // concurrency guard, not this precondition.
            var labelFrom = await SafeReadLabelAsync(ticketId, cancellationToken) ?? from;
            var labelResult = await inner.TransitionAsync(ticketId, labelFrom, to, cancellationToken);
            if (!labelResult.IsSuccess)
                logger.LogWarning(
                    "Ticket {Ticket}: DB transitioned to {To} but the {Platform} label projection did not ({Outcome}) — DB wins",
                    ticketId.Value, to, platform, labelResult.Outcome);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Ticket {Ticket}: DB transitioned to {To} but the {Platform} label projection threw — DB wins",
                ticketId.Value, to, platform);
        }

        return TransitionResult.Succeeded();
    }

    // The label's ACTUAL current lifecycle (via the platform transitioner), used
    // to anchor the projection's from-precondition. Never throws — a read failure
    // falls back to the caller's `from`, and the label stays best-effort.
    private async Task<TicketLifecycleStatus?> SafeReadLabelAsync(TicketId ticketId, CancellationToken ct)
    {
        try { return await inner.ReadCurrentAsync(ticketId, ct); }
        catch { return null; }
    }
}
