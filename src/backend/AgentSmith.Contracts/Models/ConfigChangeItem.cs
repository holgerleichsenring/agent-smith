namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-11-17-f3a8c: one configuration change the admin assistant drafted. <paramref name="Kind"/> is the Config
/// studio's route (agents, trackers, …, settings/&lt;type&gt;); <paramref name="Document"/> the entity as the studio's API
/// reads and writes it, null for a delete; <paramref name="Before"/> what the store held when it was drafted, null
/// when it held nothing — applying refuses the item when the store no longer holds exactly that.
/// </summary>
public sealed record ConfigChangeItem(string Operation, string Kind, string Id, string? Document, string? Before)
{
    public const string Upsert = "upsert";
    public const string Delete = "delete";
}
