namespace AgentSmith.Domain.Models;

/// <summary>
/// The tracker's answer to linking a child ticket to its parent. Anything but
/// <see cref="ParentLinkOutcome.Linked"/> carries the reason a person reads on the filing.
/// </summary>
public sealed record ParentLinkResult(ParentLinkOutcome Outcome, string? Reason)
{
    public static ParentLinkResult Linked { get; } = new(ParentLinkOutcome.Linked, null);

    public static ParentLinkResult Unsupported(string reason) => new(ParentLinkOutcome.Unsupported, reason);

    public static ParentLinkResult Failed(string reason) => new(ParentLinkOutcome.Failed, reason);
}
