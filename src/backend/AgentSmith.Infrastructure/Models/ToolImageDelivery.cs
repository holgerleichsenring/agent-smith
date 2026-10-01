namespace AgentSmith.Infrastructure.Models;

/// <summary>
/// 2026-10-01-283dd: whether a tool loop shows deposited images to its model, and if not,
/// why. Two facts decide it: the transport — whether the builder's client keeps an image
/// placed after a tool result — and the agent's vision flag, whether the model can see.
/// </summary>
public sealed record ToolImageDelivery(bool IsDelivered, string Reason)
{
    public const string ProviderReason = "this provider does not deliver an image after a tool result";
    public const string VisionReason = "this agent is configured without vision";

    public static ToolImageDelivery For(bool providerAcceptsImage, bool agentSeesImages) =>
        (providerAcceptsImage, agentSeesImages) switch
        {
            (false, _) => new(false, ProviderReason),
            (_, false) => new(false, VisionReason),
            _ => new(true, string.Empty),
        };
}
