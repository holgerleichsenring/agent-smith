using AgentSmith.Server.Services.Preflight;

namespace AgentSmith.Server.Extensions;

internal static class WebApplicationExtensions
{
    internal static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        // p0324: /health carries the startup preflight verdict — "pending" until the
        // warn-only run finished, then pass/fail with each failed check's message +
        // fix hint inlined. GetService (not Required) keeps the endpoint working in
        // hosts that don't register the preflight (tests building a partial app).
        app.MapGet("/health", (HttpContext ctx) => Results.Ok(new
        {
            status = "ok",
            timestamp = DateTimeOffset.UtcNow,
            preflight = PreflightHealthSection.From(
                ctx.RequestServices.GetService<PreflightReportStore>()),
        }));
        return app;
    }
}
