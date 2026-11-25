using AgentSmith.Application.Services.Holds;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-23-31dcb: the master's question a series still waits on a person for; null when none waits.
/// 2026-11-23-8d16: a question relayed from a ticket waits only while that ticket's hold is still the question — one answered
/// on the ticket's page is answered, though the record learns it only at the master's next beat. Without holds read (the
/// merge act) the record alone decides. 2026-11-25-e94ea: that next beat drops it (SeriesQuestions) and relays the next.</summary>
internal static class SeriesRunQuestion
{
    internal static string? Of(SeriesRunRecord record, IReadOnlyDictionary<string, TicketHold>? holds = null) =>
        Waiting(record, holds)?.Text;

    /// <summary>2026-11-25-9992j: the waiting question itself, with its choices and recommendation, by the same rule as
    /// <see cref="Of"/> — the series page draws its choices as the chat's buttons.</summary>
    internal static SeriesQuestion? Waiting(SeriesRunRecord record, IReadOnlyDictionary<string, TicketHold>? holds = null)
    {
        if (record.Question is not { Answer: null } waiting) return null;
        if (holds is null || waiting.TicketId is not { } ticket) return waiting;
        return holds.GetValueOrDefault(ticket)?.Case is TicketHoldCases.Question or TicketHoldCases.QuestionPlan
            or TicketHoldCases.QuestionEnvironment ? waiting : null; // a park relayed for its plan answerer is an environment hold
    }
}
