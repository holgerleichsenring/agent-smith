using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0200: HTTP control surface for in-flight pipeline runs. Today: cancel.
/// The cancel endpoint signals the per-run CancellationTokenSource the
/// executor registered at run start; the executor's in-flight sandbox-step
/// await trips and the run terminates with RunFinished(status=failed,
/// summary="cancelled"). Also publishes RunCancelRequestedEvent so the
/// dashboard reflects the intent immediately, before the terminal event
/// lands.
/// </summary>
internal static class RunControlEndpoints
{
    internal static WebApplication MapRunControlEndpoints(this WebApplication app)
    {
        app.MapPost("/api/runs/{runId}/cancel", CancelAsync);
        return app;
    }

    private static async Task<IResult> CancelAsync(
        string runId,
        IRunCancellationRegistry registry,
        IEventPublisher events,
        CancellationToken cancellationToken)
    {
        if (!registry.TryCancel(runId)) return Results.NotFound();
        var evt = new RunCancelRequestedEvent(runId, "operator", DateTimeOffset.UtcNow);
        await events.PublishAsync(evt, cancellationToken);
        return Results.Accepted();
    }
}
