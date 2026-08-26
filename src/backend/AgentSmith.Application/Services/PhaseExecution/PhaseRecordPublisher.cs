using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>
/// p0466: the phase record, to the SERVER. The working-tree copy travels to the pull request
/// and dies with the sandbox; a phase you can open after the run needs a copy the server
/// holds, and the event stream is the only channel a spawned orchestrator has to it.
/// <para>
/// 2026-08-26-31e5: its own type. Sending the record to the server is not the same
/// responsibility as writing it into a working tree, and the handler that does the writing
/// had to make room for the index line.
/// </para>
/// </summary>
public sealed class PhaseRecordPublisher(IEventPublisher eventPublisher)
{
    public Task PublishAsync(PipelineContext pipeline, PhaseDraft draft, string body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(draft);
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return Task.CompletedTask;
        return eventPublisher.PublishAsync(
            new PhaseRecordedEvent(runId, draft.PhaseId, body, DateTimeOffset.UtcNow), ct);
    }
}
