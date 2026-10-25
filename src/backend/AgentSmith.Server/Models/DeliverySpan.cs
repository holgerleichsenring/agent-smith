namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-25-0099b: one stretch of time the delivery overview counts — a period or one slot of it. The id keys are
/// the run-id prefixes of its bounds, so an attempt belongs to the span its id time falls in, in SQL and here alike.
/// </summary>
public sealed record DeliverySpan(DateTimeOffset Start, DateTimeOffset End, string FromKey, string ToKey)
{
    public bool Holds(string runId) =>
        string.CompareOrdinal(runId, FromKey) >= 0 && string.CompareOrdinal(runId, ToKey) < 0;
}
