using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Services.Lifecycle;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0200: HTTP control surface for in-flight pipeline runs. Today: cancel.
///
/// p0330: cancel is PERSISTENT STATE, not a best-effort signal. The endpoint
/// writes CancelRequested + a kill deadline onto the run row BEFORE returning;
/// the cooperative token (in-process runs) may land inside the grace window,
/// and <see cref="Services.Lifecycle.CancelEnforcer"/> force-kills anything
/// still alive after it — including spawned orchestrator pods the in-memory
/// registry can structurally never reach.
///
/// States the operator can hit:
///   1. QUEUED run — pure bookkeeping: queue entry deleted, row finished
///      'cancelled', ticket terminalized (p0330 — otherwise the next poll
///      re-claims it).
///   2. Live run — flag persisted (the enforcer's contract), registry CTS
///      signalled when the executor runs in-process.
///   3. Row-less zombie known only to the broadcaster snapshot — synthetic
///      RunFinished(cancelled) clears it (pre-relational behaviour).
///   4. Genuinely unknown runId → 404.
/// </summary>
internal static class RunControlEndpoints
{
    internal static WebApplication MapRunControlEndpoints(this WebApplication app)
    {
        app.MapPost("/api/runs/{runId}/cancel", CancelAsync).Needs(Security.Permissions.RunsControl);
        // p0327: the dashboard's answer affordance for waiting_for_input runs.
        app.MapPost("/api/runs/{runId}/answer", AnswerAsync).Needs(Security.Permissions.RunsControl);
        // p0390: the explicit operator Retry for a ticket parked on a
        // not-implementable verdict — the only way back, because a verdict does
        // not auto-retry on a comment the way a question does.
        app.MapPost("/api/runs/{runId}/retry", RetryAsync).Needs(Security.Permissions.RunsControl);
        return app;
    }

    // p0390: clears the recorded hand-back state and moves the ticket back to a
    // trigger status. The existing poller then claims it — no second launch path.
    internal static async Task<IResult> RetryAsync(
        string runId,
        RunRepository runs,
        AgentSmithConfig config,
        Services.Lifecycle.NotImplementableRetryService retry,
        CancellationToken cancellationToken)
    {
        var run = await runs.GetRunDetailAsync(runId, cancellationToken);
        if (run is null) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(run.TicketId))
            return Results.BadRequest("This run has no ticket to retry.");
        if (!config.Projects.TryGetValue(run.Project, out var project))
            return Results.BadRequest($"Project '{run.Project}' is not configured.");

        var moved = await retry.RetryAsync(project, run.TicketId!, cancellationToken);
        return moved
            ? Results.Ok(new { runId, run.TicketId, retried = true })
            : Results.BadRequest("The project's tracker declares no trigger status to move the ticket to.");
    }

    /// <summary>p0327 request body: the operator's answer text (+ optional comment).</summary>
    internal sealed record AnswerRequest(string Answer, string? Comment = null);

    // p0327: answers go through the DURABLE dialogue transport — inbox row first
    // (survives restarts; first answer wins), hot stream second. The resume
    // sweeper turns the inbox row into the actual re-launch; the endpoint only
    // delivers. Works for hot waits too: a live WaitForAnswer sees the stream.
    internal static async Task<IResult> AnswerAsync(
        string runId,
        AnswerRequest body,
        IRunCheckpointStore checkpoints,
        IDialogueTransport dialogueTransport,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.Answer)) return Results.BadRequest("answer is required");
        var checkpoint = await checkpoints.GetByRunIdAsync(runId, cancellationToken);
        if (checkpoint is null) return Results.NotFound();
        if (checkpoint.ResumedAt is not null)
            return Results.Conflict("The question was already answered — the run is resuming.");

        await dialogueTransport.PublishAnswerAsync(
            checkpoint.DialogueJobId,
            new DialogAnswer(
                checkpoint.QuestionId, body.Answer.Trim(), body.Comment,
                DateTimeOffset.UtcNow, "dashboard-operator"),
            cancellationToken);
        return Results.Accepted();
    }

    // Internal for the p0330 unit test (synchronous persistence contract).
    internal static async Task<IResult> CancelAsync(
        string runId,
        IRunCancellationRegistry registry,
        AgentSmith.Server.Services.Events.JobsBroadcaster broadcaster,
        IEventPublisher events,
        RunRepository runs,
        ICapacityQueue capacityQueue,
        CancelledTicketFinalizer ticketFinalizer,
        TimeProvider timeProvider,
        CancelTerminalWriter terminalWriter,
        CancellationToken cancellationToken)
    {
        // p0320c: a QUEUED run has no executor and holds no lease — cancelling it
        // is a pure bookkeeping move: delete the queue entry and finish the row
        // 'cancelled' via the terminal event (no registry roundtrip).
        if (await QueuedRunCancel.TryAsync(runId, runs, capacityQueue, events, ticketFinalizer,
                terminalWriter, cancellationToken))
            return Results.Accepted();

        // p0330: SYNCHRONOUS persistence — the flag + kill deadline land on the
        // row before this request returns, so navigate-away-and-back still shows
        // the cancel, and the enforcer's guarantee survives a restart.
        var persisted = await runs.MarkCancelRequestedAsync(
            runId, "operator", timeProvider.GetUtcNow() + CancelEnforcer.KillGrace, cancellationToken);

        var liveCancel = registry.TryCancel(runId, reason: "operator");
        var snapshotExists = broadcaster.Active.ContainsKey(runId);
        if (!liveCancel && !persisted && !snapshotExists) return Results.NotFound();

        await events.PublishAsync(
            new RunCancelRequestedEvent(runId, "operator", DateTimeOffset.UtcNow),
            cancellationToken);

        // p0357 (p0330b): terminalize the ticket AT REQUEST TIME for the running
        // branch too. The cooperative cancel finishes the run and releases the
        // lease WITHOUT touching the ticket, and the terminal row then makes the
        // enforcer skip its finalize — so the ticket stayed in trigger_statuses
        // and the next poll re-claimed it as a fresh run. Fail-soft inside.
        if (liveCancel || persisted)
            await FinalizeTicketAsync(runId, runs, ticketFinalizer, cancellationToken);

        // p0330: a persisted live row is the ENFORCER's job now — a premature
        // synthetic RunFinished would mark the row terminal and the enforcer
        // would skip the kill while the pod runs on. Only the row-less zombie
        // (broadcaster snapshot with no run row) still stale-clears.
        if (!liveCancel && !persisted)
        {
            await PublishStaleClearAsync(runId, events, cancellationToken);
        }
        return Results.Accepted();
    }

    private static async Task FinalizeTicketAsync(
        string runId, RunRepository runs, CancelledTicketFinalizer ticketFinalizer,
        CancellationToken cancellationToken)
    {
        var run = await runs.GetRunDetailAsync(runId, cancellationToken);
        if (run is null) return;
        await ticketFinalizer.FinalizeAsync(run.Project, run.TicketId, runId,
            "<b>Agent Smith — Cancelled</b><br/>Cancelled by operator.",
            cancellationToken);
    }

    private static Task PublishStaleClearAsync(
        string runId, IEventPublisher events, CancellationToken cancellationToken) =>
        events.PublishAsync(
            new RunFinishedEvent(
                RunId: runId,
                // p0259: the operator's action was a cancel, so the zombie clears as
                // "cancelled" — consistent with a live cancel, not a spurious failure.
                Status: "cancelled",
                PrUrl: null,
                Summary: "stale-cancelled (no executor was running this id)",
                FinishedAt: DateTimeOffset.UtcNow,
                CostUsd: null),
            cancellationToken);
}
