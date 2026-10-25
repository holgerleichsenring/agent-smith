namespace AgentSmith.Server.Services.Delivery;

/// <summary>2026-10-25-0099b: how one attempt stands, as the delivery timeline colours it.</summary>
public static class DeliveryOutcomes
{
    public const string Running = "running";
    public const string Queued = "queued";
    public const string Waiting = "waiting";
    public const string Delivered = "delivered";
    public const string Shortfall = "shortfall";
    public const string Cancelled = "cancelled";
    public const string Failed = "failed";
}
