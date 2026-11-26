using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Holds;

/// <summary>
/// 2026-11-26-f511a: a ticket of an active series that waits on another of its tickets is not yet a person's step. A draft pull
/// request stacked on a requirement that has not landed is published by the series master once that requirement is fixed, and a
/// failed attempt the master already took up (it wrote to the ticket, refreshed or retargeted its branch after the failure)
/// waits for the requirement its master is waiting on. Both hold as a prerequisite wait on nobody — Waits on another — so the
/// ticket list, the ticket page and the series' "Needs you" agree.
/// </summary>
public static class TicketSeriesWait
{
    /// <summary>The master's acts that take a ticket up: what it told the ticket's run and the branch work it started for it.</summary>
    public static bool TakesUp(string tool) =>
        tool is "tell_ticket" or SeriesPullRequestTools.RefreshTool or SeriesPullRequestTools.RetargetTool;

    /// <summary>When the master last took the ticket up (<see cref="TakesUp"/>); null when it never did.</summary>
    public static DateTimeOffset? TakenUpAt(IReadOnlyList<SeriesAct> acts, string ticketId)
    {
        ArgumentNullException.ThrowIfNull(acts);
        // A refused call is logged under its tool's name (SeriesActedFunction); only an act that went through says what it did.
        return acts.Where(a => a.Ticket == ticketId && TakesUp(a.Tool) && a.Text != a.Tool)
            .Select(a => (DateTimeOffset?)(a.DoneAt ?? a.At)).Max();
    }

    /// <summary>The ticket waits on a requirement of its active series that has not landed.</summary>
    public static bool WaitsOnAnother(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts.SeriesUnlanded.Count > 0;
    }

    /// <summary>Its newest attempt failed and the master took the ticket up after that, while it waits on another.</summary>
    public static bool TakenUp(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts.HeadStatus == RunStatuses.Failed && WaitsOnAnother(facts) && facts.SeriesTakenUpAt is { } at && facts.EndedAt is { } ended && at >= ended;
    }

    /// <summary>The prerequisite wait on nobody of the two cases above — an open draft or a failure taken up, not at work — while
    /// it waits on another; null otherwise, so a closed pull request or a cancelled run keeps its own state.</summary>
    public static TicketHold? Hold(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var draft = facts.OpenPullRequests is { Drafts.Count: > 0 };
        return WaitsOnAnother(facts) && !TicketHoldOf.AtWork(facts) && (draft || TakenUp(facts))
            ? TicketHoldOf.Hold(facts, TicketHoldAddressees.Nobody, TicketHoldCases.Prerequisite, null, facts.EndedAt) with { Names = facts.SeriesUnlanded }
            : null;
    }
}
