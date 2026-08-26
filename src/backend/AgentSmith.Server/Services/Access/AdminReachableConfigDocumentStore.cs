using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Services.Access;

/// <summary>
/// 2026-08-26-7a51: the one point every role-mapping write passes.
/// <para>
/// Written as a check on the settings route the invariant would be bypassed three ways
/// that never touch it — an import decomposes straight into the doc store, a revert writes
/// a prior document through, and the bootstrap migration writes the file's mapping in. So
/// it sits on the document store itself, where the settings save, the import, the revert
/// and the migration all arrive.
/// </para>
/// <para>
/// It judges the WHOLE document rather than the delta, which is also what makes two
/// administrators saving concurrently safe: each save is asked the same question about the
/// document it is about to become, so the second cannot pass by reference to a state the
/// first has already left.
/// </para>
/// </summary>
internal sealed class AdminReachableConfigDocumentStore(
    IConfigDocumentStore inner, AdminRoute route, ConfigDocJson json) : IConfigDocumentStore
{
    public bool IsEmpty() => inner.IsEmpty();

    public IReadOnlyList<ConfigDocRow> LoadAll() => inner.LoadAll();

    public void Save(ConfigDocWrite write)
    {
        ArgumentNullException.ThrowIfNull(write);
        Refuse(write.Type, write.Doc);
        inner.Save(write);
    }

    public void Import(IReadOnlyList<ConfigDocWrite> entities, bool force)
    {
        ArgumentNullException.ThrowIfNull(entities);
        foreach (var entity in entities) Refuse(entity.Type, entity.Doc);
        inner.Import(entities, force);
    }

    public void Delete(string type, string id, string changedBy) => inner.Delete(type, id, changedBy);

    public IReadOnlyList<ConfigDocVersion> GetVersions() => inner.GetVersions();

    public ConfigDocVersion? GetVersion(long versionId) => inner.GetVersion(versionId);

    public string? PriorDoc(string type, string id, int beforeVersion) =>
        inner.PriorDoc(type, id, beforeVersion);

    private void Refuse(string type, string doc)
    {
        if (type != ConfigDocTypes.RoleMapping) return;
        var mapping = Deserialize(doc);
        if (mapping is not null && !route.ExistsIn(mapping))
            throw new ConfigurationException(AdminRoute.Refusal);
    }

    // A document that will not parse is not this guard's refusal to make: the store's own
    // write path reports a malformed doc, and answering it here would name the wrong cause.
    private RoleMappingConfig? Deserialize(string doc)
    {
        try
        {
            return JsonSerializer.Deserialize<RoleMappingConfig>(doc, json.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
