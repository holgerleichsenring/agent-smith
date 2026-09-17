using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// What the run is told about its own cut.
/// <para>
/// p0393a needed one verdict — the split was refused and the ticket is carried whole —
/// and p0447 added the second: the split stands, and so do findings against it. Both are
/// the same statement to the same reader, so they are written in one place: an event on
/// the run, named, with the reason a person can act on.
/// </para>
/// </summary>
public sealed class SpecCutGate(IEventPublisher events, ILogger<SpecCutGate> logger)
{
    /// <summary>The cut was thrown away; one phase carries the whole ticket.</summary>
    public Task RefusedAsync(
        PipelineContext pipeline, string ticketId, string reason, CancellationToken ct)
    {
        logger.LogWarning(
            "Refusing to split ticket {Ticket}: {Reason} — one phase with the whole ticket instead",
            ticketId, reason);
        return PublishAsync(
            pipeline, "spec-accounting",
            $"{reason} — the ticket is carried whole by a single phase", ct);
    }

    /// <summary>
    /// The cut is kept and the reviewer's last objection stands against it. Not obeyed,
    /// not swallowed — a finding nobody can see is the same as no review.
    /// </summary>
    public Task KeptDespiteAsync(
        PipelineContext pipeline, string ticketId, string findings, CancellationToken ct)
    {
        logger.LogWarning(
            "Keeping the cut for ticket {Ticket} with standing review findings: {Findings}",
            ticketId, findings);
        return PublishAsync(
            pipeline, "spec-cut-review",
            $"{findings} — the cut is kept; these findings stand", ct);
    }

    /// <summary>
    /// 2026-09-17-0e79a: the run has NO usable spec — a filed ticket whose approved set never
    /// arrived, or a malformed one someone shipped. The step fails, and the same sentence is
    /// published on the run's gate trail, which is where a person reads why a run stopped;
    /// a failed step alone names the phase, not the reason.
    /// </summary>
    public async Task<CommandResult> RefuseSpecAsync(
        PipelineContext pipeline, string ticketId, string reason, CancellationToken ct)
    {
        logger.LogError("Ticket {Ticket} has no usable phase spec: {Reason}", ticketId, reason);
        await PublishAsync(pipeline, "spec-source", reason, ct);
        return CommandResult.Fail($"Ticket {ticketId} has no usable phase spec: {reason}");
    }

    private async Task PublishAsync(
        PipelineContext pipeline, string gate, string reason, CancellationToken ct)
    {
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId)
            || string.IsNullOrEmpty(runId))
            return;
        await events.PublishAsync(
            new GateCheckedEvent(runId!, gate, Passed: false, reason, DateTimeOffset.UtcNow), ct);
    }
}
