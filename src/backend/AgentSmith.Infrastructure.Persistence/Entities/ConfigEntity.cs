namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// p0349: one config entity persisted as an opaque JSON document. The taxonomy
/// (EntityType) plus EntityId is the natural key — a collection entry carries its
/// catalog id, a singleton carries the fixed id 'default'. Doc is the serialized
/// slice of the C# config model, so a new setting is zero DB migration. Version
/// is the optimistic-concurrency token bumped on every save; the full history
/// lives in <see cref="ConfigEntityVersion"/>.
/// </summary>
public sealed class ConfigEntity : EntityBase
{
    public long Id { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string Doc { get; set; } = string.Empty;

    public int Version { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;
}
