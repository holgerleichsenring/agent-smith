using AgentSmith.Application.Services.Series;

namespace AgentSmith.Application.Services.Holds;

/// <summary>
/// 2026-11-26-5201f: what a person does on the host before an open pull request of a ticket not at work can merge — publish a
/// draft (the host asks nobody to approve one), then approve it. A person's step whatever its CI says while that runs or
/// passed, its act opening the first such pull request; never a wait on nobody, so never reported overdue. A pull request the
/// series must first retarget holds as the retarget, which a draft or an approval does not lift.
/// </summary>
public static class TicketHostStepHold
{
    /// <summary>An open pull request is still a draft. A run may keep one on purpose (half-way, at a chunk boundary, no work
    /// delivered), which the hold cannot tell from a host that refused the ready mark — so its words never say "publish" alone.</summary>
    public static TicketHold? Draft(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        // 2026-11-26-f511a: a draft stacked on a requirement of its active series that has not landed is the master's to
        // publish once that lands, not a person's step yet (TicketSeriesWait).
        return Moves(facts) && !TicketSeriesWait.WaitsOnAnother(facts) && facts.OpenPullRequests is { Drafts: [var first, ..] }
            ? TicketHoldOf.Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.PrDraft, new(TicketHoldActs.PullRequest, first), null)
            : null;
    }

    /// <summary>The host still needs a person's approval on an open pull request.</summary>
    public static TicketHold? ApprovalNeeded(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return Moves(facts) && facts.OpenPullRequests is { AwaitingApproval: [var first, ..] }
            ? TicketHoldOf.Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.ApprovalNeeded, new(TicketHoldActs.PullRequest, first), null)
            : null;
    }

    private static bool Moves(TicketHoldFacts facts) =>
        !TicketHoldOf.AtWork(facts) && facts.MergeStep is not { Wait: SeriesRunMergeWaits.Retarget };
}
