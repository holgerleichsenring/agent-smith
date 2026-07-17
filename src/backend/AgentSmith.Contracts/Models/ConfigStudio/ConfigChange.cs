namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// One attributed, versioned audit row: who / when / target / diff. The diff is
/// carried as the before- and after-JSON of the affected entity, which is what
/// the revert path replays (Create→delete, Delete→re-create Before, Update→restore
/// Before). <see cref="Version"/> is a monotonic per-store sequence number so the
/// Changes view can order and address a specific revision.
/// </summary>
public sealed record ConfigChange(
    string Id,
    long Version,
    DateTimeOffset Timestamp,
    string Actor,
    ConfigEntityType EntityType,
    string EntityId,
    ConfigChangeOperation Operation,
    string? BeforeJson,
    string? AfterJson,
    bool Reverted);
