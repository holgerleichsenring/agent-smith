using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Events;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0423b: the read surface of the STORY view — why did this run do that. Separate from
/// <see cref="RunQueryEndpoints"/>' live surface on purpose: progress-watching and
/// failure-diagnosis are different jobs, and the operator opens this one deliberately.
/// <para>
/// Nothing here is pushed and nothing here is counted while the run happens. The
/// statistics are a fold over the recorded trail; the trace is the sidecar p0423 wrote.
/// </para>
/// </summary>
internal static class RunStoryEndpoints
{
    internal static WebApplication MapRunStoryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/runs/{runId}/statistics", GetStatisticsAsync).Needs(Permissions.RunsRead);
        app.MapGet("/api/runs/{runId}/trace", GetTraceAsync).Needs(Permissions.RunsRead);
        app.MapGet("/api/runs/{runId}/trace/{sequence:int}/{label}", GetTraceEntryAsync)
           .Needs(Permissions.RunsRead);
        return app;
    }

    private static async Task<IResult> GetStatisticsAsync(
        string runId, RunStatisticsReader statistics, CancellationToken cancellationToken)
    {
        var view = await statistics.ReadAsync(runId, cancellationToken);
        return Results.Ok(view);
    }

    // An untraced run answers with an empty list — the reader is ABSENT, never broken.
    private static async Task<IResult> GetTraceAsync(
        string runId, RunTraceIndexReader trace, CancellationToken cancellationToken)
    {
        var entries = await trace.ListAsync(runId, cancellationToken);
        return Results.Ok(new { entries });
    }

    private static async Task<IResult> GetTraceEntryAsync(
        string runId, int sequence, string label, RunTraceIndexReader trace,
        CancellationToken cancellationToken)
    {
        var content = await trace.ReadAsync(runId, sequence, label, cancellationToken);
        return content is null
            ? Results.NotFound()
            : Results.Ok(new { sequence, label, content });
    }
}
