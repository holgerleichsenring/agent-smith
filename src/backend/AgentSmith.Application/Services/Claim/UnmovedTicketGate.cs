using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// 2026-09-18-c1a7: refuses a claim whose ticket the last run could not move. It sits in the
/// claim service because that is the ONE door — the poller, the webhook dispatcher and the
/// capacity pump all pass through it, and a gate in the spawn funnel would leave the pump,
/// which claims its head directly. The pump's RESUME of a parked run never reaches here,
/// which is why a run that asked a question still comes back.
/// </summary>
internal sealed class UnmovedTicketGate(IUnmovedTicketStore store)
{
    /// <summary>The refusal, or null when the ticket may be claimed.</summary>
    public async Task<ClaimResult?> RefusalAsync(
        ClaimRequest request, AgentSmithConfig config, CancellationToken cancellationToken)
    {
        var tracker = config.Projects[request.ProjectName].Tracker.Name;
        var standing = await store.FindStandingAsync(
            request.ProjectName, request.TicketId.Value, tracker, cancellationToken);
        return standing is null
            ? null
            : ClaimResult.Rejected(
                ClaimRejectionReason.TicketLastLeftUnmoved,
                $"The last run could not move this ticket to '{standing.ConfiguredStatus}' "
                + $"({standing.Outcome}). Correct that status on tracker '{standing.Tracker}' "
                + $"or on project '{standing.Project}' — saving either releases this ticket. "
                + "The usual cause is the WORK-ITEM TYPE rather than the status: a tracker that "
                + "configures no work_item_kinds files everything as the provider's default type, "
                + "and a status that type does not have can never be reached.");
    }
}
