using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// 2026-08-25-1806: moves an installation's existing role mapping out of its bootstrap file
/// and into the config store, ONCE. An installation with roles and group_roles under
/// <c>auth:</c> has a working mapping today, and a release that simply stopped reading that
/// file would lock out everyone the mapping let in.
/// <para>
/// It runs before the server answers its first request, and it is the only thing that says
/// the store is now authoritative: while it has not spoken, the file governs. A store that
/// already holds a mapping is never overwritten — the studio's copy is the newer one.
/// </para>
/// </summary>
internal sealed class RoleMappingMigration(
    IConfigDocumentStore documents,
    TokenAuthorityConfig auth,
    RoleMappingSource source,
    ConfigDocJson json,
    IStartupFindings findings,
    ILogger<RoleMappingMigration> logger)
{
    private const string Actor = "bootstrap-migration";

    public void Run()
    {
        try
        {
            Import();
            source.AdoptStore();
        }
        catch (Exception ex)
        {
            // The seed keeps governing this process: a store that could not be reached is a
            // reason to keep the mapping the installation already had, never to drop it.
            logger.LogError(ex, "The role mapping could not be migrated into the config store");
            findings.Record(Unmigrated(ex));
        }
    }

    private void Import()
    {
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

    private static StartupFinding Unmigrated(Exception ex) => new(
        StartupSubsystems.Configuration,
        StartupFindingSeverity.Advisory,
        "The role mapping in the bootstrap config could not be moved into the config store, "
        + "so it is still the file that decides what a role means and the Config Studio "
        + $"cannot change it. Cause: {ex.Message}",
        Field: "role_mapping");
}
