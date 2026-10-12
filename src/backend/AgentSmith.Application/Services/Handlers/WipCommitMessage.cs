using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Lifecycle;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-12-3a36c: the ONE message a failed run commits its work under — the WIP subject the
/// PR sweep recognises as ours, plus the Run-Id, Pipeline and Failed-Step trailers. Used by the
/// work-branch persist and by CommitAndPR on a failed run, so a failed run never says 'fix:'.
/// </summary>
public sealed class WipCommitMessage
{
    private const string Unknown = "unknown";

    public string Build(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var runId = pipeline.TryGet<string>(ContextKeys.RunId, out var rid) && !string.IsNullOrEmpty(rid)
            ? rid : Unknown;
        var pipelineName = pipeline.TryGet<ResolvedPipelineConfig>(
            ContextKeys.ResolvedPipeline, out var resolved) && resolved is not null
            ? resolved.PipelineName : Unknown;
        var failedStep = pipeline.TryGet<string>(ContextKeys.FailedStepName, out var fs) && !string.IsNullOrEmpty(fs)
            ? fs : Unknown;
        return $"{WipCommit.Subject(runId!)}\n\n" +
               $"Run-Id: {runId}\n" +
               $"Pipeline: {pipelineName}\n" +
               $"Failed-Step: {failedStep}\n";
    }
}
