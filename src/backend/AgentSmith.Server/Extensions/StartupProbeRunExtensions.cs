using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Startup;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-29-7f2d: the startup probe pass, run once the listener is bound. The probes only report, and
/// each was bounded at ten seconds while it held the port shut; the findings endpoint waits for the pass.
/// </summary>
internal static class StartupProbeRunExtensions
{
    internal static WebApplication RunProbesAfterStart(this WebApplication app)
    {
        // The findings list and the pass are resolved here, while the container is certainly alive: a host
        // stopped right after it started disposes the container under a pass still running.
        var findings = app.Services.GetService<IStartupFindings>();
        var pass = app.Services.GetService<StartupProbePass>();
        var stopping = app.Lifetime.ApplicationStopping;
        app.Lifetime.ApplicationStarted.Register(() => _ = Task.Run(() => RunProbesAsync(app, findings, pass, stopping)));
        return app;
    }

    // p0391b: this is the first eager singleton resolution, and it drags in the loaded
    // configuration, the Redis multiplexer and the composed sandbox backend. The runner turns a
    // probe that throws into a finding — but only once it exists, so a failure to BUILD it
    // was still an unreported dead process. The findings list is registered unconditionally
    // and needs nothing, so it can always carry the reason.
    private static async Task RunProbesAsync(
        WebApplication app, IStartupFindings? findings, StartupProbePass? pass, CancellationToken stopping)
    {
        try
        {
            await app.Services.GetRequiredService<IStartupProbeRunner>().RunAsync(stopping);
        }
        catch (Exception) when (stopping.IsCancellationRequested)
        {
            // Shutting down: nobody is left to read what the pass would have found.
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Startup probes could not run");
            findings?.Record(new StartupFinding(
                StartupSubsystems.Configuration, StartupFindingSeverity.Blocking,
                "The startup probes could not run, so nothing else on this page was checked. "
                + $"Cause: {ex.Message}"));
        }
        finally
        {
            pass?.Complete();
        }
    }
}
