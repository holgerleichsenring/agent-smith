using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Init;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0489: the dashboard's start-an-initialization surface. Init only — a generic
/// start-any-pipeline endpoint was declined, so there is no pipeline parameter and
/// no untested launch path for the other presets. The answers are the launcher's
/// outcomes: the run id on success, 409 with the LIVE run id when an init of this
/// project is already going, 503 with the budget's reason when it does not fit, and
/// 400 when no such project is configured.
/// <para>
/// 2026-10-02-5f89d: the GET answers the project's live init run — so the button restores
/// itself after navigation — under runs.read, the permission every other run read needs:
/// 200 with the run and its state, 204 when none is live, 404 when no such project is
/// configured. A launch that finds another launch of the project mid-flight answers 409.
/// </para>
/// </summary>
internal static class ProjectInitEndpoints
{
    internal static WebApplication MapProjectInitEndpoints(this WebApplication app)
    {
        app.MapPost("/api/projects/{name}/init", InitAsync).Needs(Permissions.ProjectsInit);
        app.MapGet("/api/projects/{name}/init", GetStateAsync).Needs(Permissions.RunsRead);
        return app;
    }

    // Internal so the p0489 endpoint tests drive the real launcher without a host.
    // p0490: the body carries the operator's auto-accept for THIS launch; a request
    // without one does not auto-accept. 2026-10-04-2bf2: likewise refresh principles.
    internal static async Task<IResult> InitAsync(
        string name, InitLaunchRequest? request, InitRunLauncher launcher,
        CancellationToken cancellationToken)
    {
        var result = await launcher.LaunchAsync(
            name, request ?? new InitLaunchRequest(), cancellationToken);
        var body = new InitLaunchResponse(result.RunId, result.Reason);
        return result.Outcome switch
        {
            InitLaunchOutcome.Started => Results.Ok(body),
            InitLaunchOutcome.AlreadyRunning or InitLaunchOutcome.BeingStarted => Results.Conflict(body),
            InitLaunchOutcome.NoCapacity =>
                Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.BadRequest(body),
        };
    }

    // Internal so the 2026-10-02-5f89d endpoint tests drive the real reader without a host.
    internal static async Task<IResult> GetStateAsync(
        string name, InitRunStateReader reader, CancellationToken cancellationToken)
    {
        var lookup = await reader.ReadAsync(name, cancellationToken);
        if (!lookup.IsKnownProject) return Results.NotFound();
        return lookup.Live is null ? Results.NoContent() : Results.Ok(lookup.Live);
    }
}
