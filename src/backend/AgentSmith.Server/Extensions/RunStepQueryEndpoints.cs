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

    /// <summary>
    /// p0388d: one step's events, in the direction the caller asks for.
    /// <c>sinceSeq</c> (positive) is the live poll's forward delta;
    /// <c>beforeSeq</c> walks backwards into history; neither means "the newest
    /// page", which is what opening a step now returns.
    ///
    /// <para><c>sinceSeq=0</c> is deliberately NOT a forward read: a caller with
    /// no cursor holds nothing, and reading forward from nothing is exactly the
    /// defect this phase removes — it hands back the FIRST page of a step that
    /// has since produced thousands of rows. It reads as "the newest page".</para>
    ///
    /// <para>The response carries only the flag the direction established:
    /// a forward delta knows nothing about older rows, and saying
    /// <c>hasOlder: false</c> there would overwrite what the caller already
    /// learned from its opening read.</para>
    /// </summary>
    internal static async Task<IResult> GetRunStepEventsAsync(
        string runId, int stepIndex, long? sinceSeq, long? beforeSeq, int? limit,
        TrailReader trailReader, CancellationToken cancellationToken)
    {
        if (sinceSeq is > 0)
        {
            var delta = await trailReader.ReadStepForwardAsync(
                runId, stepIndex, sinceSeq.Value, limit, cancellationToken);
            return Results.Ok(new
            {
                events = delta.Events, newestSeq = delta.NewestSeq, hasNewer = delta.HasNewer,
            });
        }

        var page = await trailReader.ReadStepBackwardsAsync(
            runId, stepIndex, beforeSeq, limit, cancellationToken);
        return Results.Ok(new
        {
            events = page.Events,
            oldestSeq = page.OldestSeq,
            newestSeq = page.NewestSeq,
            hasOlder = page.HasOlder,
        });
    }

    internal static async Task<IResult> GetRunDecisionsAsync(
        string runId, int? limit, RunDecisionsReader decisions, CancellationToken cancellationToken)
    {
        var latest = await decisions.ReadLatestAsync(
            runId, Math.Clamp(limit ?? DefaultDecisionLimit, 1, MaxDecisionLimit), cancellationToken);
        return Results.Ok(new { decisions = latest });
    }
}
