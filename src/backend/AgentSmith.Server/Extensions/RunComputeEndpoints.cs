using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Events;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-13-9a59i: a run's spawns — every sandbox with its times and how it ended — and the sandboxes it
/// does not count.
/// </summary>
internal static class RunComputeEndpoints
{
    internal static WebApplication MapRunComputeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/runs/{runId}/compute", ComputeAsync).Needs(Permissions.RunsRead);
        return app;
    }

    // Internal so the endpoint tests drive the real reader without a host.
    internal static async Task<IResult> ComputeAsync(string runId, RunComputeReader reader, CancellationToken cancellationToken)
    {
        var view = await reader.ReadAsync(runId, cancellationToken);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }
}
