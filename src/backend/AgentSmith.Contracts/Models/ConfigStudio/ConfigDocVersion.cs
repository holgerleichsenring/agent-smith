namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// p0349: one row of the single config audit — the state of an entity at a given
/// version. A null <see cref="Doc"/> is a delete tombstone. <see cref="Id"/> is the
/// stable audit-row id used to address a revert.
/// </summary>
public sealed record ConfigDocVersion(
    long Id,
    string Type,
    string EntityId,
    int Version,
    string? Doc,
    string ChangedBy,
    string? Note,
    DateTimeOffset ChangedAt);
