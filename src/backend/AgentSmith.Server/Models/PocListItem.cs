namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-933da: one PoC card — its host is made from its target's domain_pattern, null without one.</summary>
public sealed record PocListItem(
    long Id, string Name, string Project, string Target, string? Host, string State, string OwnerName, string? LastRunId,
    int? LatestRevision = null)
{
    /// <summary>2026-11-23-c81ee: its lifetime — the card's 'expires in N days' and 'expired' badge.</summary>
    public PocLifetimeView? Lifetime { get; init; }
}
