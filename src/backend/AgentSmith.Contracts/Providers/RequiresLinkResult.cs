namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-10-14-dbcem: what a tracker made of one requires link. A reason is a CLASS — "not authorized", "the tracker
/// refused the link (HTTP 404)" — never tracker text, which carries organisation and host names. Unreachable marks a
/// tracker that did not answer, the one result that ends a link pass.
/// </summary>
public sealed record RequiresLinkResult(bool Linked, string? Reason, bool Unreachable)
{
    public static RequiresLinkResult Done { get; } = new(true, null, false);

    public static RequiresLinkResult Refused(string reason) => new(false, reason, false);

    public static RequiresLinkResult Unreached(string reason) => new(false, reason, true);
}
