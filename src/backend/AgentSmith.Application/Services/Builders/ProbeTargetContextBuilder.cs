using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// 2026-09-01-379a: builds the ProbeTarget context. Like EnsurePrerequisites, the handler
/// reads everything it needs (sandboxes + the per-sandbox context list) from the pipeline,
/// so the builder only forwards the pipeline.
/// </summary>
public sealed class ProbeTargetContextBuilder : IContextBuilder
{
    public ICommandContext Build(
        PipelineCommand command, ResolvedProject project, PipelineContext pipeline) =>
        new ProbeTargetContext(pipeline);
}
