using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0327: publishes a run checkpoint (pending question + step cursor +
/// serialized context) when a dialogue wait crosses the hot threshold.
/// </summary>
public interface IDialogueCheckpointWriter
{
    /// <summary>Returns false when the run cannot checkpoint (no run id / step
    /// cursor / ticket) — the caller then falls back to the timeout default.</summary>
    Task<bool> TryCheckpointAsync(
        PipelineContext pipeline, DialogQuestion question, string dialogueJobId,
        CancellationToken cancellationToken);
}
