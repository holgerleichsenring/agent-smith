using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Resume;

/// <summary>
/// 2026-08-25-a508: resolves the dialogue identity for every ask path of a run.
/// <para>
/// The identity is the run id. Both ask paths call this, so a checkpoint written by one is
/// read back by the other under the same key.
/// </para>
/// </summary>
public sealed class DialogueJobIdentity : IDialogueJobIdentity
{
    public string? Resolve(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<string>(ContextKeys.RunId, out var runId) && !string.IsNullOrEmpty(runId)
            ? runId
            : null;
    }
}
