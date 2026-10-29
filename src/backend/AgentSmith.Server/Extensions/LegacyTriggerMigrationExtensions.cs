using AgentSmith.Server.Services.Startup;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-29-6bd3a: moves the stored per-project trigger blocks onto their trackers once the server
/// LISTENS, not before. Unlike the preset-alias migration nothing a request does depends on it — every
/// load folds the same blocks — so it has no reason to hold the port shut.
/// </summary>
internal static class LegacyTriggerMigrationExtensions
{
    internal static WebApplication MigrateLegacyTriggersAfterStart(this WebApplication app)
    {
        app.Lifetime.ApplicationStarted.Register(() => _ = Task.Run(() =>
        {
            try
            {
                app.Services.GetRequiredService<LegacyTriggerMigration>().Run();
            }
            catch (Exception ex)
            {
                // Resolving it is the only failure Run() cannot report on its own behalf.
                app.Logger.LogInformation(ex, "The trigger-block migration could not be resolved");
            }
        }));
        return app;
    }
}
