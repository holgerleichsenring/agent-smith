namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-14-dbcen: one ticket on either side of a requires, with its landing where one is stored.
/// <paramref name="LandedBy"/> is the landing's By as stored — the person's display name, written at marking time.
/// </summary>
/// <param name="Key">The name a person calls it by: a Jira id as it is, every other tracker's number behind a hash.</param>
/// <param name="State">As a graph node's state.</param>
public sealed record TicketRequirementView(
    string TicketId, string Key, string Title, string State, bool HasPage,
    string? LandedKind = null, string? LandedBy = null, DateTimeOffset? LandedAt = null, string? Note = null);
