using AgentSmith.Server.Services.Startup;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-09-25-e5b1: rewrites the stored configuration off the retired preset names BEFORE the
/// listener serves anything — a request answered in between would be routed by a name that no
/// longer resolves, which is the one failure the rewrite exists to prevent.
/// <para>
/// Like the role-mapping migration it is not an <c>IStartupProbe</c>: a probe reports on a
/// dependency and this one writes. It reports what it could not do through the same findings list.
/// </para>
/// </summary>
internal static class PipelineAliasMigrationExtensions
{
    internal static WebApplication MigratePipelineAliases(this WebApplication app)
    {
        try
        {
            app.Services.GetRequiredService<PipelineAliasMigration>().Run();
        }
        catch (Exception ex)
        {
            // Resolving it is the only failure Run() cannot report on its own behalf — an
            // installation with no document store has no stored configuration to move.
            app.Logger.LogInformation(ex, "The preset-alias migration could not be resolved");
        }
        return app;
    }
}
