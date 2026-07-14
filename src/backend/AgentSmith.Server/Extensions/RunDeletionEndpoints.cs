using AgentSmith.Server.Hubs;
using AgentSmith.Server.Services.Lifecycle;
using Microsoft.AspNetCore.SignalR;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0337: destructive run cleanup — delete a single run in any state, or bulk
/// "clear terminal runs". Gated behind the UI-API (AGENTSMITH_UI_API_ENABLED)
/// like the query endpoints, since it is a dashboard affordance — unlike the
/// always-on cancel/answer control surface in <see cref="RunControlEndpoints"/>.
/// A successful delete fires a RunsChanged nudge so every dashboard drops the
/// run on its next refetch.
/// </summary>
internal static class RunDeletionEndpoints
{
    internal static WebApplication MapRunDeletionEndpoints(this WebApplication app)
    {
        app.MapDelete("/api/runs/{runId}", DeleteRunAsync);
        app.MapDelete("/api/runs", ClearTerminalAsync);
        return app;
    }

    internal static async Task<IResult> DeleteRunAsync(
        string runId,
        RunDeleter deleter,
        IHubContext<JobsHub> hub,
        CancellationToken cancellationToken)
    {
        var outcome = await deleter.DeleteAsync(runId, cancellationToken);
        if (outcome == RunDeleteOutcome.NotFound) return Results.NotFound();
        if (outcome == RunDeleteOutcome.PodTerminationFailed)
            return Results.Problem(
                "Could not terminate the run's pod — the record was kept. Retry once the backend is reachable.",
                statusCode: StatusCodes.Status502BadGateway);
        await NudgeAsync(hub, runId, cancellationToken);
        return Results.NoContent();
    }

    internal static async Task<IResult> ClearTerminalAsync(
        RunDeleter deleter,
        IHubContext<JobsHub> hub,
        string? state,
        CancellationToken cancellationToken)
    {
        // Bulk delete is scoped to terminal runs only — the single supported filter.
        if (!string.Equals(state, "terminal", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest("only state=terminal is supported for bulk delete");
        var deleted = await deleter.DeleteTerminalAsync(cancellationToken);
        await NudgeAsync(hub, "bulk", cancellationToken);
        return Results.Ok(new { deleted });
    }

    private static Task NudgeAsync(IHubContext<JobsHub> hub, string runId, CancellationToken ct) =>
        hub.Clients.Group(HubGroups.Overview).SendAsync("RunsChanged", runId, ct);
}
