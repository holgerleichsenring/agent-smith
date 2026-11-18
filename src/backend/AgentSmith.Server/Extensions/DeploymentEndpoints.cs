using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Deployment;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-18-3863c: a person deploys a context of a project to one environment — runs.control in the route's project,
/// like every act that starts a run; 200 carries the run id, a refusal is a plain sentence. The GET lists the project's
/// deployment bindings under runs.read, for the dashboard's deploy act.
/// </summary>
internal static class DeploymentEndpoints
{
    internal static WebApplication MapDeploymentEndpoints(this WebApplication app)
    {
        app.MapPost("/api/projects/{project}/deployments", DeployAsync)
            .Needs(Permissions.RunsControl).InProject(ProjectFrom.Route("project"));
        app.MapGet("/api/projects/{project}/deployments", List)
            .Needs(Permissions.RunsRead).InProject(ProjectFrom.Route("project"));
        return app;
    }

    // Internal so the endpoint tests drive the real launcher without a host.
    internal static async Task<IResult> DeployAsync(
        string project, DeployLaunchRequest? request, DeployLauncher launcher, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Context) || string.IsNullOrWhiteSpace(request.Environment))
            return Results.Text("a deploy names a context and an environment", "text/plain", statusCode: StatusCodes.Status400BadRequest);
        var launch = await launcher.LaunchAsync(project, request, cancellationToken);
        return launch.Outcome switch
        {
            DeployLaunchOutcome.Started => Results.Ok(new { runId = launch.RunId }),
            DeployLaunchOutcome.UnknownProject => Results.Text(launch.Reason, "text/plain", statusCode: StatusCodes.Status404NotFound),
            _ => Results.Text(launch.Reason, "text/plain", statusCode: StatusCodes.Status409Conflict),
        };
    }

    internal static IResult List(string project, DeploymentBindings bindings) =>
        bindings.Of(project) is { } list ? Results.Ok(list) : Results.NotFound();
}
