using AgentSmith.Contracts.Events;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Events;

/// <summary>
/// Maps <see cref="StepEvent"/>s (Sandbox.Wire — agent-side) into L3
/// <see cref="RunEvent"/>s (Contracts.Events — server-side). Single place
/// where the seam between the two deployments crosses; keeps the projector
/// itself small.
/// </summary>
internal static class StepEventToRunEventMapper
{
    public static SandboxOutputEvent? AsOutput(StepEvent stepEvent, string runId, string repo, long batchSeq)
        => stepEvent.Kind switch
        {
            StepEventKind.Stdout => new SandboxOutputEvent(runId, repo, "stdout", stepEvent.Line, batchSeq, stepEvent.Timestamp),
            StepEventKind.Stderr => new SandboxOutputEvent(runId, repo, "stderr", stepEvent.Line, batchSeq, stepEvent.Timestamp),
            _ => null
        };
}
