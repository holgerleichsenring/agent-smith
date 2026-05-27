using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Convenience accessors for well-known typed values stored in
/// <see cref="PipelineContext"/>.
/// </summary>
public static class PipelineContextExtensions
{
    /// <summary>
    /// Returns the merged per-pipeline configuration produced by
    /// <c>IPipelineConfigResolver</c> at the top of the pipeline.
    /// Throws when called outside an executing pipeline (must always be set).
    /// </summary>
    public static ResolvedPipelineConfig Resolved(this PipelineContext pipeline) =>
        pipeline.Get<ResolvedPipelineConfig>(ContextKeys.ResolvedPipeline);
}
