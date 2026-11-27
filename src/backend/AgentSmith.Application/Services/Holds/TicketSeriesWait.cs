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

    /// <summary>2026-11-27-ecf2e: when the master's start of the ticket was last refused because the series' approver may not
    /// start runs — a start_ticket call on a series that may start none; null otherwise.</summary>
    public static DateTimeOffset? StartRefusedAt(SeriesRunRecord record, string ticketId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.MayStartRuns) return null;
        return record.Acts.Where(a => a.Ticket == ticketId && a.Tool == "start_ticket").Select(a => (DateTimeOffset?)a.At).Max();
    }

    /// <summary>2026-11-27-ecf2e (operator): a never-run ticket whose start the master was refused, waiting on nothing else, is a
    /// person's step — they start it themselves (its series' page offers Start); null otherwise.</summary>
    public static TicketHold? StartRefused(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts is { SeriesStartRefusedAt: { } at, HeadStatus: null, WaitsFor.Count: 0 } && !WaitsOnAnother(facts) && !TicketHoldOf.AtWork(facts)
            ? TicketHoldOf.Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.StartRefused, new(TicketHoldActs.Series, facts.SeriesId), at)
            : null;
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
