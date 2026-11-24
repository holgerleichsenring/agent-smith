using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>2026-11-23-c81ed: the seeds and the preset the PocAct step starts from.</summary>
public sealed class PocActContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline) =>
        new PocActContext(pipeline, project,
            pipeline.TryGet<string>(ContextKeys.PipelineName, out var preset) ? preset : null, PocSeeds.From(pipeline));
}
