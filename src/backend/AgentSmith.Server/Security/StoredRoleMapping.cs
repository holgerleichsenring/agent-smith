using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-08-25-1806: reads the role mapping out of the config store, which for the server is
/// the DB entity-document store the Config Studio writes. The read is in-memory — the store
/// holds its assembled document and reassembles it on every write — so asking per request
/// costs a lock and a field read, and a save is visible to the very next call.
/// </summary>
internal sealed class StoredRoleMapping(IConfigStore store, ILogger<StoredRoleMapping> logger)
    : IStoredRoleMapping
{
    public RoleMappingConfig? Read()
    {
        try
        {
            return store.GetSetting(ConfigDocTypes.RoleMapping) as RoleMappingConfig;
        }
        catch (Exception ex)
        {
            // A store that cannot be read is a reason to keep the mapping the installation
            // booted with, never a reason to strip every caller of their roles.
            logger.LogWarning(ex, "The stored role mapping could not be read — using the bootstrap seed");
            return null;
        }
    }
}
