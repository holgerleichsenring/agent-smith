using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Builders;

public sealed class AcquireSourceContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
        => new AcquireSourceContext(project.Source, pipeline);
}

public sealed class BootstrapDocumentContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new BootstrapDocumentContext(repo, project.Agent, project.SkillsPath, pipeline);
    }
}

public sealed class DeliverOutputContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        pipeline.TryGet<string>(ContextKeys.OutputFormat, out var outputFormat);
        return new DeliverOutputContext(project.Source, repo, pipeline, outputFormat);
    }
}
