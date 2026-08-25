using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Resume;

/// <summary>
/// 2026-08-25-a508: resolves the dialogue identity for every ask path of a run.
/// <para>
/// The progress reporter's job id wins when the run has one (a spawned orchestrator's
/// question/answer streams are keyed on it); an in-process server run has no --job-id, so the
/// run id is the identity. Both ask paths call this, so a checkpoint written by one is read
/// back by the other under the same key.
/// </para>
/// </summary>
public sealed class DialogueJobIdentity(IProgressReporter progressReporter) : IDialogueJobIdentity
{
    public string? Resolve(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (progressReporter.JobId is { Length: > 0 } jobId) return jobId;
        return pipeline.TryGet<string>(ContextKeys.RunId, out var runId) && !string.IsNullOrEmpty(runId)
            ? runId
            : null;
    }
}
