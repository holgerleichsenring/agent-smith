using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// 2026-10-08-e8b9b: extracted from NotImplementableRetryService, which now calls it, so the Retry
/// and the rework entry clear exactly the same three things: the hand-back case (otherwise the next
/// attempt's repeat guard reads "handed back again"), the approval's SatisfiedAt (2026-10-06-03c7f:
/// discovery names the ticket again) and the unmoved fact (2026-09-18-c1a7: the claim gate reads it).
/// </summary>
public sealed class TicketReopener(
    ISpecSetPointerStore pointers,
    ISpecApprovalStore approvals,
    IUnmovedTicketStore unmovedTickets) : ITicketReopener
{
    public async Task ClearAsync(ResolvedProject project, string ticketId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        var key = TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;
        var pointer = await pointers.GetAsync(project.Name, key, cancellationToken);
        if (pointer is not null)
            await pointers.SaveAsync(project.Name, pointer with
            {
                LastHandbackCase = SpecHandbackCase.None,
                RepeatedHandbackCount = 0,
            }, cancellationToken);
        await approvals.ReopenAsync(project.Tracker.Name, key, cancellationToken);
        await unmovedTickets.ClearAsync(project.Name, ticketId, cancellationToken);
    }
}
