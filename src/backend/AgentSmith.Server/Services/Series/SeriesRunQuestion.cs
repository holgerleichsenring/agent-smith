using AgentSmith.Application.Services.Holds;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-23-31dcb: the master's question a series still waits on a person for; null when none waits.
/// 2026-11-23-8d16: a question relayed from a ticket waits only while that ticket's hold is still the question — one answered
/// on the ticket's page is answered, though the record learns it only at the master's next beat. Without holds read (the
/// merge act) the record alone decides.</summary>
internal static class SeriesRunQuestion
{
    internal static string? Of(SeriesRunRecord record, IReadOnlyDictionary<string, TicketHold>? holds = null)
    {
        if (record.Question is not { Answer: null } waiting) return null;
        if (holds is null || waiting.TicketId is not { } ticket) return waiting.Text;
        return holds.GetValueOrDefault(ticket)?.Case is TicketHoldCases.Question or TicketHoldCases.QuestionPlan
            or TicketHoldCases.QuestionEnvironment ? waiting.Text : null; // a park relayed for its plan answerer is an environment hold
    }
}
