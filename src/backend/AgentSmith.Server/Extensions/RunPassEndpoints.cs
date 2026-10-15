using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Passes;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-13-9a59f: a run's master passes per spec — why each opened and ended, its verdicts, the steps
/// it moved and its cost — and where the rest of the run's model spend went.
/// </summary>
internal static class RunPassEndpoints
{
    internal static WebApplication MapRunPassEndpoints(this WebApplication app)
    {
        app.MapGet("/api/runs/{runId}/passes", PassesAsync).Needs(Permissions.RunsRead).InProject(ProjectFrom.Run);
        return app;
    }

    // Internal so the endpoint tests drive the real reader without a host.
    internal static async Task<IResult> PassesAsync(string runId, RunPassesReader reader, CancellationToken cancellationToken)
    {
        var view = await reader.ReadAsync(runId, cancellationToken);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }
}
