using AgentSmith.Server.Services.Startup;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-08-25-1806: moves an installation's file-declared role mapping into the config
/// store, eagerly and BEFORE the listener starts serving — a mapping that migrated after
/// the first request would have answered that request from the file and the next one from
/// an empty store.
/// <para>
/// It is deliberately not an <c>IStartupProbe</c>: a probe reports on a dependency and this
/// one writes. It reports what it could not do the same way, through the findings list.
/// </para>
/// </summary>
internal static class RoleMappingMigrationExtensions
{
    internal static WebApplication MigrateRoleMapping(this WebApplication app)
    {
        try
        {
            app.Services.GetRequiredService<RoleMappingMigration>().Run();
        }
        catch (Exception ex)
        {
            // Resolving it is the only failure Run() cannot report on its own behalf.
            app.Logger.LogError(ex, "The role-mapping migration could not be resolved");
        }
        return app;
    }
}
