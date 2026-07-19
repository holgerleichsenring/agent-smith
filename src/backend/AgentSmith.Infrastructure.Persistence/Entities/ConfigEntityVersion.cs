namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// p0349: the SINGLE attributed, revertible config audit — one row per version of
/// a config entity. A null <see cref="Doc"/> is a delete tombstone; a non-null doc
/// is the entity's state at that version. Reverting replays a prior row's doc as a
/// new version, so the trail is append-only and every change is who/when/what.
/// </summary>
public sealed class ConfigEntityVersion : EntityBase
{
    public long Id { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public int Version { get; set; }

    public string? Doc { get; set; }

    public string ChangedBy { get; set; } = string.Empty;

    public string? Note { get; set; }
}
