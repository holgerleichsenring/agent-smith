using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Holds;

/// <summary>
/// 2026-11-23-31dch: the hold of a parked question, by who can answer it. Outside a series that leads the ticket, an
/// environment question is the admin's — the person sees it reported there — and any other is a person's decision on the
/// ticket.
/// <para>2026-11-27-3532b: in an active series that is no series of one, every parked question — the target's, the plan's,
/// the environment's — is its series master's: one Question hold, its act the series' conversation, addressed to nobody,
/// so it is no admin hold and no person's "needs you". The master answers it or asks the operator in that conversation.</para>
/// </summary>
public static class TicketQuestionHold
{
    /// <summary>The parked question of a series-led ticket: its series master's.</summary>
    public static TicketHold? Master(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return Waiting(facts) && SeriesLed(facts)
            ? TicketHoldOf.Hold(facts, TicketHoldAddressees.Nobody, TicketHoldCases.Question,
                new TicketHoldAct(TicketHoldActs.Conversation, facts.SeriesId), facts.ParkedAt)
            : null;
    }

    /// <summary>The parked questions name the environment: reported to the admin, who answers once it is fixed.</summary>
    public static TicketHold? Environment(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (!Waiting(facts) || SeriesLed(facts) || !facts.Answerers.Contains(QuestionAnswerers.Environment)) return null;
        return TicketHoldOf.Hold(facts, TicketHoldAddressees.Admin, TicketHoldCases.QuestionEnvironment,
            new(TicketHoldActs.Answer), facts.ParkedAt);
    }

    /// <summary>The parked question is the ticket's own: answered on it. Null for a series-led ticket's.</summary>
    public static TicketHold? Target(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return Waiting(facts) && !SeriesLed(facts)
            ? TicketHoldOf.Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.Question, new(TicketHoldActs.Answer), facts.ParkedAt)
            : null;
    }

    private static bool Waiting(TicketHoldFacts facts) => facts.Parked && facts.Asked;

    // SeriesActive is SeriesLeads' rule (TicketHoldSeriesRead): active, no series of one, a master that can act.
    private static bool SeriesLed(TicketHoldFacts facts) => facts.SeriesId is not null && facts.SeriesActive;
}
