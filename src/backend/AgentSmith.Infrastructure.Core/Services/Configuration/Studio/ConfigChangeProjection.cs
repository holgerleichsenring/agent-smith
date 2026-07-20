using AgentSmith.Contracts.Models.ConfigStudio;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// p0349: projects the single config audit (config_entity_version rows) onto the
/// studio's <see cref="ConfigChange"/> feed. Studio-editable collection types AND the
/// global settings singletons surface in the Changes view; a settings row is addressed
/// by its settings-type key (all singletons share the 'default' doc id). The audit-row
/// id is the address a revert targets.
/// </summary>
internal static class ConfigChangeProjection
{
    public static IReadOnlyList<ConfigChange> From(IReadOnlyList<ConfigDocVersion> versions)
    {
        var changes = new List<ConfigChange>();
        foreach (var version in versions)
        {
            if (!TryMapType(version.Type, out var entityType)) continue;
            changes.Add(new ConfigChange(
                Id: version.Id.ToString(),
                Version: version.Version,
                Timestamp: version.ChangedAt,
                Actor: version.ChangedBy,
                EntityType: entityType,
                // Settings singletons are keyed by their type ('orchestrator', 'limits', …);
                // the underlying doc id is the shared 'default'.
                EntityId: entityType == ConfigEntityType.Settings ? version.Type : version.EntityId,
                Operation: OperationOf(version),
                BeforeJson: null,
                AfterJson: version.Doc,
                Reverted: false));
        }
        return changes;
    }

    private static ConfigChangeOperation OperationOf(ConfigDocVersion version) =>
        version.Doc is null ? ConfigChangeOperation.Delete
        : version.Version <= 1 ? ConfigChangeOperation.Create
        : ConfigChangeOperation.Update;

    private static bool TryMapType(string type, out ConfigEntityType entityType)
    {
        (var mapped, entityType) = type switch
        {
            ConfigDocTypes.Agent => (true, ConfigEntityType.Agent),
            ConfigDocTypes.Tracker => (true, ConfigEntityType.Tracker),
            ConfigDocTypes.Repo => (true, ConfigEntityType.Repo),
            ConfigDocTypes.Project => (true, ConfigEntityType.Project),
            ConfigDocTypes.McpServer => (true, ConfigEntityType.McpServer),
            ConfigDocTypes.Secret => (true, ConfigEntityType.Secret),
            ConfigDocTypes.Connection => (true, ConfigEntityType.Connection),
            _ when ConfigSettingsAccess.Types.Contains(type) => (true, ConfigEntityType.Settings),
            _ => (false, default),
        };
        return mapped;
    }
}
