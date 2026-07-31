using AgentSmith.Server.Services.Events;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0388b: the run detail's full pipeline, served as BOUNDED queries against the
/// DB projections. The rail is one row per step; a step's body is one clamped
/// page fetched when the step is selected; the decisions list is the latest N.
/// What a response ships is O(visible), never O(runtime) — the invariant the
/// client-side event fold could not hold.
/// </summary>
internal static class RunStepQueryEndpoints
{
    // The Building beat shows a handful of notes; the ceiling keeps a hand-edited
    // limit from turning the notes list into a full decision export.
    private const int DefaultDecisionLimit = 20;
    private const int MaxDecisionLimit = 100;

    internal static WebApplication MapRunStepQueryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/runs/{runId}/steps", GetRunStepsAsync);
        app.MapGet("/api/runs/{runId}/steps/{stepIndex:int}/events", GetRunStepEventsAsync);
        app.MapGet("/api/runs/{runId}/decisions", GetRunDecisionsAsync);
        return app;
    }

    internal static async Task<IResult> GetRunStepsAsync(
        string runId, RunStepsReader steps, CancellationToken cancellationToken)
    {
        var rail = await steps.ReadAsync(runId, cancellationToken);
        return Results.Ok(new { steps = rail });
    }

    internal static async Task<IResult> GetRunStepEventsAsync(
        string runId, int stepIndex, long? sinceSeq, int? limit,
        TrailReader trailReader, CancellationToken cancellationToken)
    {
        var page = await trailReader.ReadStepPageAsync(
            runId, stepIndex, sinceSeq ?? 0, limit, cancellationToken);
        return Results.Ok(new { events = page.Events, nextSeq = page.NextSeq, hasMore = page.HasMore });
    }

    internal static async Task<IResult> GetRunDecisionsAsync(
        string runId, int? limit, RunDecisionsReader decisions, CancellationToken cancellationToken)
    {
        var latest = await decisions.ReadLatestAsync(
            runId, Math.Clamp(limit ?? DefaultDecisionLimit, 1, MaxDecisionLimit), cancellationToken);
        return Results.Ok(new { decisions = latest });
    }
}
