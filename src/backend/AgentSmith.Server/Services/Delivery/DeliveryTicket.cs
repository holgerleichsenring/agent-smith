namespace AgentSmith.Server.Services.Delivery;

/// <summary>
/// 2026-10-25-0099b: one ticket's attempts in a span, oldest first, and the merge that followed its first one
/// inside the span; null when none did.
/// </summary>
public sealed record DeliveryTicket(
    string Project, string TicketId, IReadOnlyList<DeliveryAttempt> Attempts, DateTimeOffset? MergedAt)
{
    public DeliveryAttempt First => Attempts[0];

    public DeliveryAttempt Newest => Attempts[^1];
}
