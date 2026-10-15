using AgentSmith.Server.Hubs;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Events;
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
        app.MapDelete("/api/runs/{runId}", DeleteRunAsync).Needs(Permissions.RunsDelete).InProject(ProjectFrom.Run);
        app.MapDelete("/api/runs", ClearTerminalAsync).Needs(Permissions.RunsDelete).InProject(ProjectFrom.AcrossProjects);
        return app;
    }

    internal static async Task<IResult> DeleteRunAsync(
        string runId,
        RunDeleter deleter,
        OverviewAudience audience,
        RunProjectIndex runs,
        IHubContext<JobsHub> hub,
        CancellationToken cancellationToken)
    {
        // 2026-10-15-4a4dc: the audience is read BEFORE the row goes, or only global readers would hear it.
        var audienceOfRun = await audience.OfRunAsync(runId, null, cancellationToken);
        var outcome = await deleter.DeleteAsync(runId, cancellationToken);
        if (outcome == RunDeleteOutcome.NotFound) return Results.NotFound();
        runs.Forget(runId);
        await NudgeAsync(hub, audienceOfRun, runId, cancellationToken);
        return Results.NoContent();
    }

    internal static async Task<IResult> ClearTerminalAsync(
        RunDeleter deleter,
        OverviewAudience audience,
        IHubContext<JobsHub> hub,
        string? state,
        CancellationToken cancellationToken)
    {
        // Bulk delete is scoped to terminal runs only — the single supported filter.
        if (!string.Equals(state, "terminal", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest("only state=terminal is supported for bulk delete");
        var deleted = await deleter.DeleteTerminalAsync(cancellationToken);
        await NudgeAsync(hub, audience.Everyone(), "bulk", cancellationToken);
        return Results.Ok(new { deleted });
    }

    private static Task NudgeAsync(IHubContext<JobsHub> hub, IReadOnlyList<string> groups, string runId, CancellationToken ct) =>
        hub.Clients.Groups(groups).SendAsync("RunsChanged", runId, ct);
}
