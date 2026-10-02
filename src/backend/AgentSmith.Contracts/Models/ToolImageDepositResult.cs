namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-10-01-283dd: whether a deposited <see cref="ToolImage"/> was taken. A refusal names
/// why, so the producing tool can say so in its own text instead of promising a picture.
/// </summary>
public sealed record ToolImageDepositResult(bool IsAccepted, string? Refusal)
{
    public static ToolImageDepositResult Accepted { get; } = new(true, null);

    public static ToolImageDepositResult Refused(string reason) => new(false, reason);
}
