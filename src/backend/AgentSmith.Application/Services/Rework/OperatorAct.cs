using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// 2026-10-13-9a59m: when an approval in a ticket's own conversation is an act on the ticket. The stored
/// approval IS the act — nothing else is persisted for it: it counts only when its continuation says it
/// starts an attempt, the attempt did not publish it (or a newer one) as worked, and it may be after what
/// the attempt read. With no attempt at all, a starting approval is the act that starts the first one.
/// </summary>
public static class OperatorAct
{
    public static ReworkAct? Of(SpecApprovalRecord? record, PreviousAttempt? attempt)
    {
        if (record?.Continuation is not { StartsAttempt: true } || record.Approval is not { } approval) return null;
        if (attempt is not null
            && (attempt.WorkedApprovalAt is { } worked && approval.At <= worked || !attempt.Precedes(approval.At)))
            return null;
        return new ReworkAct(approval.Principal, approval.At, ReworkChannel.Operator)
        {
            Source = new AttemptSource(AttemptSourceKinds.Conversation, Id: approval.Conversation),
        };
    }
}
