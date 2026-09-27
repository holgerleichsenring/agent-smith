using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Polling;

/// <summary>
/// 2026-09-25-3c7aa: the envelope a POLLED ticket routes on. It carries labels, ticket id and
/// platform — a poll has no area path and no source repository — plus whether an approved
/// specification is recorded for it, which is the one part that costs a read.
/// <para>
/// Its own file because the poller is over the line limit and may only get shorter: the envelope
/// is a shape with one question in it, and the loop around it is a different responsibility.
/// </para>
/// </summary>
public sealed class PolledTicketEnvelope(ApprovedRecordProbe approvals)
{
    public async Task<IncomingTicketEnvelope> ForAsync(
        TrackerConnection tracker, Ticket ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(ticket);
        var platform = tracker.Type.ToString().ToLowerInvariant();
        return new IncomingTicketEnvelope
        {
            Labels = ticket.Labels,
            TicketId = ticket.Id.Value,
            Platform = platform,
            HasApprovedRecord =
                await approvals.ExistsAsync(tracker.Name, platform, ticket.Id.Value, cancellationToken),
        };
    }
}
