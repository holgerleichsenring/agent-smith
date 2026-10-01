using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>2026-10-01-283df: builds the MaterializeReferenceSets context — the pipeline is all it needs.</summary>
public sealed class MaterializeReferenceSetsContextBuilder : IContextBuilder
{
    public ICommandContext Build(
        PipelineCommand command, ResolvedProject project, PipelineContext pipeline) =>
        new MaterializeReferenceSetsContext(pipeline);
}
