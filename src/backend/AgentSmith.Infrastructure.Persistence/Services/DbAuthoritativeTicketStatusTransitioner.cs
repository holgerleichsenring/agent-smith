using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
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
    DbTicketLifecycleStore store,
    string project,
    string platform,
    ILogger logger) : ITicketStatusTransitioner
{
    public string ProviderType => inner.ProviderType;

    public async Task<TicketLifecycleStatus?> ReadCurrentAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        var dbStatus = await store.GetStatusAsync(project, platform, ticketId, cancellationToken);
        if (dbStatus is not null && Enum.TryParse<TicketLifecycleStatus>(dbStatus, out var parsed))
            return parsed;
        return await inner.ReadCurrentAsync(ticketId, cancellationToken);
    }

    public async Task<TransitionResult> TransitionAsync(
        TicketId ticketId, TicketLifecycleStatus from, TicketLifecycleStatus to, CancellationToken cancellationToken)
    {
        // DB first — authoritative.
        await store.SetStatusAsync(project, platform, ticketId, to.ToString(), cancellationToken);

        // Label as a best-effort projection. A failure is logged, never fatal.
        try
        {
            var labelResult = await inner.TransitionAsync(ticketId, from, to, cancellationToken);
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
}
