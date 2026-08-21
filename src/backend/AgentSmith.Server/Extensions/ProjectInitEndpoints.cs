using AgentSmith.Server.Services.Init;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0489: the dashboard's start-an-initialization surface. Init only — a generic
/// start-any-pipeline endpoint was declined, so there is no pipeline parameter and
/// no untested launch path for the other presets. The answers are the launcher's
/// outcomes: the run id on success, 409 with the LIVE run id when an init of this
/// project is already going, 503 with the budget's reason when it does not fit, and
/// 400 when no such project is configured.
/// </summary>
internal static class ProjectInitEndpoints
{
    internal static WebApplication MapProjectInitEndpoints(this WebApplication app)
    {
        app.MapPost("/api/projects/{name}/init", InitAsync);
        return app;
    }

    // Internal so the p0489 endpoint tests drive the real launcher without a host.
    // p0490: the body carries the operator's auto-accept for THIS launch; a request
    // without one does not auto-accept.
    internal static async Task<IResult> InitAsync(
        string name, InitLaunchRequest? request, InitRunLauncher launcher,
        CancellationToken cancellationToken)
    {
        var result = await launcher.LaunchAsync(
            name, request?.AutoCompletePullRequests ?? false, cancellationToken);
        var body = new InitLaunchResponse(result.RunId, result.Reason);
        return result.Outcome switch
        {
            InitLaunchOutcome.Started => Results.Ok(body),
            InitLaunchOutcome.AlreadyRunning => Results.Conflict(body),
            InitLaunchOutcome.NoCapacity =>
                Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.BadRequest(body),
        };
    }
}
