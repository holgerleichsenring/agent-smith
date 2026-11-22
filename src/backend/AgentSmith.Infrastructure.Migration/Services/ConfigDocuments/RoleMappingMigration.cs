using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Migration.Services.ConfigDocuments;

/// <summary>
/// 2026-08-25-1806: moves an installation's existing role mapping out of its bootstrap file
/// and into the config store, ONCE. A store that already holds a mapping is never overwritten —
/// the studio's copy is the newer one.
/// <para>
/// 2026-11-22-747da: a data migration of `database migrate`, no longer a step of the server's start. The server reads
/// the store and falls back to the same file seed while the store holds none, so this import decides what the studio
/// shows, not which mapping is in force. A failure is logged and left unrecorded: the next migrate tries again.
/// </para>
/// </summary>
public sealed class RoleMappingMigration(
    TokenAuthorityConfig auth, ConfigDocJson json, ILogger<RoleMappingMigration> logger) : IDataMigration
{
    private const string Actor = "bootstrap-migration";

    public string Id => "2026-08-25-1806-role-mapping-import";

    public string? ShippedWith => null;

    public Task<bool> RunAsync(AgentSmithDbContext db, CancellationToken ct) =>
        Task.FromResult(TryImport(new ContextConfigDocumentStore(db)));

    /// <summary>Import that logs a failure instead of throwing; false when it did not complete.</summary>
    public bool TryImport(IConfigDocumentStore documents)
    {
        try
        {
            Import(documents);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The role mapping could not be migrated into the config store");
            return false;
        }
    }

    /// <summary>Imports the file's mapping unless the store holds one or the file declares none.</summary>
    public void Import(IConfigDocumentStore documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        if (documents.LoadAll().Any(row => row.Type == ConfigDocTypes.RoleMapping)) return;
        var seed = RoleMappingConfig.From(auth);
        if (seed.IsEmpty) return;

        documents.Save(new ConfigDocWrite(
            ConfigDocTypes.RoleMapping, ConfigDocTypes.SingletonId,
            JsonSerializer.Serialize(seed, json.Options),
            ExpectedVersion: null, Edges: [], Actor, "migrated from the bootstrap auth block"));
        logger.LogInformation(
            "Migrated the bootstrap role mapping into the config store: {Roles} custom role(s), "
            + "{Groups} group mapping(s)", seed.Roles.Count, seed.GroupRoles.Count);
    }
}
