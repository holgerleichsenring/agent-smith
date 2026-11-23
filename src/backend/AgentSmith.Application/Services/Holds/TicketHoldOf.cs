using AgentSmith.Contracts.Runs;

namespace AgentSmith.Application.Services.Holds;

/// <summary>2026-11-23-d60d: how every hold source builds its hold and tells a ticket at work, moved out of
/// <see cref="TicketHoldReader"/> when the pull-request holds joined it (<see cref="TicketPullRequestHold"/>).</summary>
internal static class TicketHoldOf
{
    /// <summary>The newest attempt runs, waits in the queue, or relaunches — not parked on a person — or the restart takes its
    /// interruption up: its pull request holds nothing while an attempt is (about to be) at work on it.</summary>
    internal static bool AtWork(TicketHoldFacts facts) =>
        facts.HeadStatus is RunStatuses.Running or RunStatuses.Queued || (facts.HeadStatus == RunStatuses.WaitingForInput && !facts.Parked)
        || (facts.Interrupted && facts.Restarting);

    // A stop's hold quotes what the stop recorded; a parked or a moving ticket's names no end.
    internal static TicketHold Hold(TicketHoldFacts facts, string addressee, string @case, TicketHoldAct? act, DateTimeOffset? since) =>
        new(addressee, @case, act, since)
        {
            Attempt = facts.Attempt,
            Detail = addressee is TicketHoldAddressees.Admin or TicketHoldAddressees.Decide && !facts.Parked ? facts.EndDetail : null,
        };
}
