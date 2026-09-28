using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Preflight;

namespace AgentSmith.Server.Extensions;

internal static class WebApplicationExtensions
{
    internal static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        // /health always answers 200: it is the liveness probe, and a degraded server must still
        // report itself. The body carries the startup preflight verdict and every registered
        // subsystem's state with its reason. GetService (not Required) keeps the endpoint working
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
        return app;
    }
}
