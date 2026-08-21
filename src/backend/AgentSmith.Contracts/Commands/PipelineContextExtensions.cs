using System.Text.Json;
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

    /// <summary>
    /// p0490: reads a boolean that rode in on the LAUNCH request. A value seeded
    /// in-process is a <c>bool</c>, but the same value enqueued through the Redis job
    /// queue comes back as a <see cref="JsonElement"/> — <c>PipelineRequest.Context</c>
    /// is a dictionary of <c>object</c>, and that is what System.Text.Json makes of one
    /// (the same round-trip p0327's resume payload works around by riding as a string).
    /// Absent, or anything that is not a true boolean, reads as false.
    /// </summary>
    public static bool Flag(this PipelineContext pipeline, string key)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (pipeline.TryGet<bool>(key, out var value)) return value;
        return pipeline.TryGet<JsonElement>(key, out var element)
            && element.ValueKind == JsonValueKind.True;
    }
}
