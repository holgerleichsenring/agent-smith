using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

public sealed class AcquireSourceContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
        => new AcquireSourceContext(project.Source, pipeline);
}

public sealed class BootstrapDocumentContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var resolved = pipeline.Resolved();
        return new BootstrapDocumentContext(repo, resolved.Agent, resolved.SkillsPath, pipeline);
    }
}

public sealed class DeliverOutputContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        pipeline.TryGet<string>(ContextKeys.OutputFormat, out var outputFormat);
        return new DeliverOutputContext(project.Source, repo, pipeline, outputFormat);
    }
}
