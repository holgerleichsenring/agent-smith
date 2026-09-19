using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Owns Azure DevOps' finalize and the classification that goes with it. AzDO is the one
/// tracker of the four that cannot be asked in advance: the work item's type owns its state
/// list and the rule engine answers only when the PATCH arrives. So the classification is the
/// rule engine's OWN exception type — never its message text, and never the whole SDK family:
/// a throttle, a 503 or a rotated token is a failure that may pass, and recording it would
/// refuse every later claim over a status name that is perfectly correct. Those propagate.
/// </summary>
public sealed class AzureDevOpsTicketFinalizer(
    string defaultState,
    Func<TicketId, string, string, CancellationToken, Task> write,
    ILogger logger)
{
    public async Task<TicketFinalizeResult> FinalizeAsync(
        TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken)
    {
        var state = string.IsNullOrWhiteSpace(doneStatus) ? defaultState : doneStatus!;
        try
        {
            await write(ticketId, comment, state, cancellationToken);
            return TicketFinalizeResult.Moved();
        }
        catch (RuleValidationException ex)
        {
            logger.LogWarning(ex,
                "Azure DevOps refused state '{State}' for work item {Ticket} — the ticket did not move",
                state, ticketId.Value);
            return TicketFinalizeResult.Rejected(state, nameof(RuleValidationException));
        }
    }
}
