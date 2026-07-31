using AgentSmith.Server.Hubs;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0391a: the routing table, split into what is always there and what the dashboard
/// gate adds. Health and the webhook/run-control surface are unconditional — they are the
/// channel through which a degraded server reports itself, and nothing about them depends
/// on a database, a queue or a valid configuration.
/// </summary>
internal static class ServerEndpointExtensions
{
    internal static WebApplication MapServerEndpoints(this WebApplication app)
    {
        app.MapHealthEndpoints()
           .MapSlackEndpoints()
           .MapTeamsEndpoints()
           .MapWebhookEndpoints()
           .MapRunControlEndpoints();
        return app;
    }

    internal static WebApplication MapDashboardApi(this WebApplication app)
    {
        app.UseCors(DashboardConstants.CorsPolicy);
        app.MapHub<JobsHub>("/hub/jobs");
        app.MapRunQueryEndpoints();
        app.MapPullRequestQueryEndpoints(); // p0347: the Pull Requests page read surface
        app.MapRunDeletionEndpoints(); // p0337: dashboard run cleanup (destructive, UI-API-gated)
        app.MapExpectationMetricsEndpoints();
        app.MapCatalogEndpoints();
        app.MapConfigQueryEndpoints();
        app.MapConfigStudioEndpoints(); // p0345: config studio CRUD + audit/revert
        app.MapDiagnosticsEndpoints();
        app.UseSwagger(o => o.RouteTemplate = "api/openapi/{documentName}.json");
        app.MapGet("/api/openapi.json", () => Results.Redirect("/api/openapi/v1.json", permanent: false))
           .ExcludeFromDescription();
        return app;
    }
}
