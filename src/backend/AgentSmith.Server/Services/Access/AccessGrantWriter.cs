using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Services.Access;

/// <summary>
/// 2026-08-26-7a51: the access surface's save. The WHOLE role-mapping document arrives,
/// because a settings write deserialises onto a fresh model and every field the body omits
/// would revert to its initializer default — a people-only save would reset the role claim
/// to <c>roles</c> and empty a Keycloak-shaped installation's directory roles.
/// <para>
/// It goes through the same settings path every other config write takes, so the change is
/// attributed, versioned, revertible and epoch-signalled, and the admin invariant on the
/// document store applies to it like it applies to an import.
/// </para>
/// </summary>
internal sealed class AccessGrantWriter(
    IConfigStore store, RoleMappingSource mapping, NewCustomRoleGuard customRoles, ConfigDocJson json)
{
    public void Save(JsonElement doc, ChangeAttribution by)
    {
        customRoles.Against(mapping.Current().Mapping, Bind(doc));
        store.SaveSetting(ConfigDocTypes.RoleMapping, doc, by);
    }

    /// <summary>Persists a mapping this server built itself — a removal, not an edited form.</summary>
    public void Save(RoleMappingConfig edited, ChangeAttribution by) =>
        store.SaveSetting(
            ConfigDocTypes.RoleMapping, JsonSerializer.SerializeToElement(edited, json.Options), by);

    private RoleMappingConfig Bind(JsonElement doc)
    {
        try
        {
            return doc.Deserialize<RoleMappingConfig>(json.Options) ?? new RoleMappingConfig();
        }
        catch (JsonException ex)
        {
            throw new ConfigurationException($"Invalid access document: {ex.Message}");
        }
    }
}
