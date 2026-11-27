using AgentSmith.Contracts.Series;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-23-31dcb: the master's question a series still waits on a person for; null when none waits.
/// 2026-11-27-3532b: only the master asks the operator — a ticket's question relayed before is none the series waits on
/// (its next beat drops it).</summary>
internal static class SeriesRunQuestion
{
    internal static string? Of(SeriesRunRecord record) => Waiting(record)?.Text;

    /// <summary>2026-11-25-9992j: the waiting question itself, with its choices and recommendation, by the same rule as
    /// <see cref="Of"/> — the series page draws its choices as the chat's quick replies.</summary>
    internal static SeriesQuestion? Waiting(SeriesRunRecord record) =>
        record.Question is { Answer: null, TicketId: null } waiting ? waiting : null;
}
