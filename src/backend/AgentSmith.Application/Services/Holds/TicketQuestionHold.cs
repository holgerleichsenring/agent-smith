using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Holds;

/// <summary>
/// 2026-11-23-31dch: the hold of a parked question, by who can answer it. An environment question is the admin's — the
/// person sees it reported there; a park that asks about the plan, in a series still running, is answered in that
/// series' conversation, never on the ticket, because one answer covers every question of a park; any other question,
/// and a plan question outside an active series, is a person's decision on the ticket.
/// </summary>
public static class TicketQuestionHold
{
    /// <summary>The parked questions name the environment: reported to the admin, who answers once it is fixed — in the
    /// series' conversation when the park also asks about its plan.</summary>
    public static TicketHold? Environment(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (!Waiting(facts) || !facts.Answerers.Contains(QuestionAnswerers.Environment)) return null;
        var act = PlanInSeries(facts) ? new TicketHoldAct(TicketHoldActs.Conversation, facts.SeriesId) : new(TicketHoldActs.Answer);
        return TicketHoldReader.Hold(facts, TicketHoldAddressees.Admin, TicketHoldCases.QuestionEnvironment, act, facts.ParkedAt);
    }

    /// <summary>The parked question is the ticket's own: answered on it. Null for a park its series settles.</summary>
    public static TicketHold? Target(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return Waiting(facts) && !PlanInSeries(facts)
            ? TicketHoldReader.Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.Question, new(TicketHoldActs.Answer), facts.ParkedAt)
            : null;
    }

    /// <summary>The park asks about the plan and the ticket's series still runs: talked through in its conversation.</summary>
    public static TicketHold? Plan(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return Waiting(facts) && PlanInSeries(facts)
            ? TicketHoldReader.Hold(facts, TicketHoldAddressees.Plan, TicketHoldCases.QuestionPlan,
                new(TicketHoldActs.Conversation, facts.SeriesId), facts.ParkedAt)
            : null;
    }

    private static bool Waiting(TicketHoldFacts facts) => facts.Parked && facts.Asked;

    // A question stored before the field existed carries no answerer and is the target's.
    private static bool PlanInSeries(TicketHoldFacts facts) =>
        facts.SeriesId is not null && facts.SeriesActive && facts.Answerers.Contains(QuestionAnswerers.Plan);
}
