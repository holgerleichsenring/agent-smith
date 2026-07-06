using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Drives a lifecycle transition through Jira's native workflow (GET /transitions →
/// match by mapped status name → POST) for the states an operator named in
/// <c>lifecycle_status_names</c>. Returns true only when the target state is mapped
/// AND a matching workflow transition was posted; on false the caller records the
/// state via labels instead, so no lifecycle change is ever silently lost.
/// </summary>
internal sealed class JiraNativeLifecycleTransitioner(
    JiraTransitioner transitioner, JiraLifecycleStatusMap map)
{
    public async Task<bool> TryTransitionAsync(
        TicketId ticketId, TicketLifecycleStatus to, CancellationToken cancellationToken)
        => map.TryNameFor(to, out var statusName)
           && await transitioner.TransitionAsync(ticketId, statusName, null, cancellationToken);
}
