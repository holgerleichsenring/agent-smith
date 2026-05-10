using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>Builds a <see cref="PipelineNameInitializerContext"/> from the active pipeline state.</summary>
public sealed class PipelineNameInitializerContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline) =>
        new PipelineNameInitializerContext(pipeline);
}
