using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Events;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-10-d854c: a design turn's steps, served from its run after the turn ended or the server restarted —
/// run-wide, a page at a time after <c>afterSeq</c>, with the run's step count. Read like the run's own step events.
/// </summary>
internal static class DialogActivityEndpoints
{
    internal static WebApplication MapDialogActivityEndpoints(this WebApplication app)
    {
        app.MapGet("/api/runs/{runId}/dialog-activity", GetDialogActivityAsync)
            .Needs(Permissions.RunsRead).InProject(ProjectFrom.Run);
        return app;
    }

    internal static async Task<IResult> GetDialogActivityAsync(
        string runId, long? afterSeq, int? limit, DialogActivityReader reader, CancellationToken cancellationToken) =>
        Results.Ok(await reader.ReadAsync(runId, afterSeq, limit, cancellationToken));
}
