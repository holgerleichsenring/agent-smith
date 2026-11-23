using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Preflight;
using AgentSmith.Server.Services.Startup;

namespace AgentSmith.Server.Extensions;

internal static class WebApplicationExtensions
{
    internal static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        // 2026-11-22-747db: liveness. The process answers; it resolves no service and reads nothing, so a probe of it
        // never builds a worker or waits on a dependency. Startup and liveness probes point here.
        app.MapGet("/health/live", () => Results.Ok(new { status = "live" }))
            .Anonymous("a liveness probe cannot authenticate");
        // /health always answers 200: a degraded server must still report itself. Probes use /health/live and
        // /health/ready; this one resolves the subsystem states and may build a worker on request. The body
        // carries the startup preflight verdict and every registered subsystem's state with its reason. GetService (not Required) keeps the endpoint working
        // in hosts that don't register the preflight (tests building a partial app).
        app.MapGet("/health", (HttpContext ctx) =>
        {
            var subsystems = ctx.RequestServices.GetServices<ISubsystemHealth>().ToList();
            return Results.Ok(new
            {
                status = SubsystemHealthSection.Status(subsystems),
                timestamp = DateTimeOffset.UtcNow,
                preflight = PreflightHealthSection.From(
                    ctx.RequestServices.GetService<PreflightReportStore>()),
                subsystems = SubsystemHealthSection.From(subsystems),
            });
        }).Anonymous(
            "a liveness probe cannot authenticate, and a degraded server must still report itself");
        // 2026-08-24-035a: readiness. Not ready only until the first startup probe pass has run or its
        // budget is spent; after that it stays ready, degraded or not, because this pod is the one that
        // reports the fault and must not be pulled out of the service for having one.
        app.MapGet("/health/ready", (HttpContext ctx) =>
        {
            var pass = ctx.RequestServices.GetService<StartupProbePass>();
            return pass is null || pass.IsSettled
                ? Results.Ok(new { status = "ready" })
                : Results.Json(new
                {
                    status = "starting",
                    reason = "The first startup probe pass has not finished; the server is ready once it has, "
                        + $"or after {StartupProbePass.ReadBudget.TotalSeconds:0} s (StartupProbePass.ReadBudget, "
                        + "the bound every probe pass reader shares).",
                }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }).Anonymous("a readiness probe cannot authenticate");
        return app;
    }
}
