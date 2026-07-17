using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// Thread-safe, append-only audit trail held in memory. Real and unit-tested:
/// it is the writer behind FileConfigStore's mutations. A DbConfigStore would
/// swap this for the <c>config_changes</c> table (see
/// <see cref="ConfigStudioSchema"/>); the interface is identical so the store
/// code does not change.
/// </summary>
public sealed class InMemoryConfigAuditStore : IConfigAuditStore
{
    private readonly object _gate = new();
    private readonly List<ConfigChange> _changes = new();
    private long _version;

    public ConfigChange Append(
        string actor,
        ConfigEntityType entityType,
        string entityId,
        ConfigChangeOperation operation,
        string? beforeJson,
        string? afterJson)
    {
        lock (_gate)
        {
            var change = new ConfigChange(
                Id: Guid.NewGuid().ToString("N"),
                Version: ++_version,
                Timestamp: DateTimeOffset.UtcNow,
                Actor: actor,
                EntityType: entityType,
                EntityId: entityId,
                Operation: operation,
                BeforeJson: beforeJson,
                AfterJson: afterJson,
                Reverted: false);
            _changes.Add(change);
            return change;
        }
    }

    public IReadOnlyList<ConfigChange> GetAll()
    {
        lock (_gate)
            return _changes.OrderByDescending(c => c.Version).ToList();
    }

    public ConfigChange? GetById(string id)
    {
        lock (_gate)
            return _changes.FirstOrDefault(c => c.Id == id);
    }

    public void MarkReverted(string id)
    {
        lock (_gate)
        {
            var index = _changes.FindIndex(c => c.Id == id);
            if (index >= 0)
                _changes[index] = _changes[index] with { Reverted = true };
        }
    }
}
