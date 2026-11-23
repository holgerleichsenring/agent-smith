using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Application.Services.Holds;

/// <summary>
/// 2026-11-23-d60d: the hold of a ticket's open pull requests (<see cref="TicketOpenPullRequests"/>) — "in review" is a
/// system state, not a task. Red CI is a person's decision naming the failed checks; one outside any series' merge order that
/// can merge now (CI green or none, nothing it requires unlanded) is a merge hold that opens it; one that waits is nobody's —
/// behind the unlanded tickets it requires, or while its CI runs. A series' next merge keeps the series' own act.
/// </summary>
public static class TicketPullRequestHold
{
    /// <summary>Red CI on an open pull request of a ticket that is not at work.</summary>
    public static TicketHold? CiFailed(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return !TicketHoldOf.AtWork(facts) && facts.OpenPullRequests is { Ci: CiStates.Red } red
            ? TicketHoldOf.Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.CiFailed, Act(red), red.CiSince) with { Names = red.FailedChecks }
            : null;
    }

    /// <summary>The CI of an open pull request still runs or failed: no merge is offered.</summary>
    public static bool CiHolds(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts.OpenPullRequests is { Ci: CiStates.Pending or CiStates.Red };
    }

    /// <summary>An open pull request outside any series' merge order with nothing it requires unlanded: merge it now, since
    /// its CI turned green (else since the attempt ended).</summary>
    public static TicketHold? Ready(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts.MergeStep is null && facts.OpenPullRequests is { Unlanded.Count: 0 } open
            ? TicketHoldOf.Hold(facts, TicketHoldAddressees.Merge, TicketHoldCases.MergeReady, Act(open), open.CiSince ?? facts.EndedAt)
            : null;
    }

    /// <summary>An open pull request that waits on nobody: outside a series' merge order behind the unlanded tickets it
    /// requires (since its attempt ended), or with its CI still running (since the run began).</summary>
    public static TicketHold? Waiting(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.MergeStep is null && facts.OpenPullRequests is { Unlanded.Count: > 0 } stacked)
            return TicketHoldOf.Hold(facts, TicketHoldAddressees.Nobody, TicketHoldCases.MergeAfter, null, facts.EndedAt)
                with { Names = stacked.Unlanded };
        return facts.OpenPullRequests is { Ci: CiStates.Pending } running
            ? TicketHoldOf.Hold(facts, TicketHoldAddressees.Nobody, TicketHoldCases.CiRunning, null, running.CiSince)
            : null;
    }

    // The one open pull request on its host; several open name no single one.
    private static TicketHoldAct Act(TicketOpenPullRequests open) => new(TicketHoldActs.PullRequest, open.Url);
}
