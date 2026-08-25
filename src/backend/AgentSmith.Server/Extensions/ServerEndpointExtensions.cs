using AgentSmith.Server.Hubs;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0391a: the routing table, split into what is always there and what the dashboard
/// gate adds. Health and the webhook surface are unconditional — they are the channel
/// through which a degraded server reports itself, and nothing about them depends on a
/// database, a queue or a valid configuration.
/// <para>
/// p0506: run control is NOT one of those. cancel already 500s with the dashboard off
/// (its JobsBroadcaster lives only in AddDashboardApi), while answer and retry resolved
/// fine from the unconditional chain — so an unauthenticated caller could publish an
/// answer to a master blocked on a question and move a parked ticket back into a trigger
/// status. All three ride the dashboard gate now; that is a holding position until
/// p0503a gives each route the permission it needs.
/// </para>
/// </summary>
internal static class ServerEndpointExtensions
{
    internal static WebApplication MapServerEndpoints(this WebApplication app)
    {
        app.MapHealthEndpoints()
           .MapStartupFindingsEndpoints()
           // 2026-08-25-4530: unconditional for the findings route's reason — it explains
           // an installation whose dashboard cannot reach anything else.
           .MapAuthRequirementsEndpoints()
           .MapSlackEndpoints()
           .MapTeamsEndpoints()
           .MapWebhookEndpoints();
        return app;
    }

    internal static WebApplication MapDashboardApi(this WebApplication app)
    {
        app.UseCors(DashboardConstants.CorsPolicy);
        app.MapHub<JobsHub>("/hub/jobs").Needs(Permissions.RunsRead);
        app.MapRunControlEndpoints(); // p0506: cancel / answer / retry
        app.MapRunQueryEndpoints();
        app.MapPullRequestQueryEndpoints(); // p0347: the Pull Requests page read surface
        app.MapRunDeletionEndpoints(); // p0337: dashboard run cleanup (destructive, UI-API-gated)
        app.MapExpectationMetricsEndpoints();
        app.MapCatalogEndpoints();
        app.MapConfigQueryEndpoints();
        app.MapConfigStudioEndpoints(); // p0345: config studio CRUD + audit/revert
        app.MapProjectInitEndpoints(); // p0489: start init-project for a configured project
        app.MapDiagnosticsEndpoints();
        app.MapIdentityEndpoints(); // p0503d: what this caller's token carried
        app.UseSwagger(o => o.RouteTemplate = "api/openapi/{documentName}.json");
        app.MapGet("/api/openapi.json", () => Results.Redirect("/api/openapi/v1.json", permanent: false))
           .ExcludeFromDescription()
           .Anonymous("the document that describes the routes is read before a caller has a token");
        return app;
    }
}
