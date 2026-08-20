using AgentSmith.Contracts.Events;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Events;

/// <summary>
/// The progress hook <see cref="SandboxEventProjector"/> hands the sandbox: it passes each
/// StepEvent on to the caller, publishes it as an L3 SandboxOutput for the live drawer, and
/// keeps it in the bounded failure tail. p0491 lifted it out of the projector — one type per
/// file, and the projector had no room left for the lag guard.
/// </summary>
internal sealed class ProjectingProgress(
    IProgress<StepEvent>? upstream,
    IEventPublisher eventPublisher,
    string runId,
    string repo,
    Func<long> nextSeq,
    OutputTailBuffer tail) : IProgress<StepEvent>
{
    public void Report(StepEvent value)
    {
        upstream?.Report(value);
        var seq = nextSeq();
        var outputEvent = StepEventToRunEventMapper.AsOutput(value, runId, repo, seq);
        if (outputEvent is null) return;
        // p0367: retain the line in the bounded tail for a possible failure capture.
        tail.Add(outputEvent.Line);
        // Fire-and-forget: IProgress.Report is synchronous; we mustn't block
        // the sandbox thread. Errors are swallowed (publisher logs them).
        _ = eventPublisher.PublishAsync(outputEvent, CancellationToken.None);
    }
}
