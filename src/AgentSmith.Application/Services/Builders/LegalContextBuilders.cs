using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

public sealed class AcquireSourceContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
        => new AcquireSourceContext(
            pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos)[0],
            pipeline);
}

public sealed class BootstrapDocumentContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var resolved = pipeline.Resolved();
        return new BootstrapDocumentContext(repo, resolved.Agent, resolved.SkillsPath, pipeline);
    }
}

public sealed class DeliverOutputContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var repos = pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos);
        pipeline.TryGet<string>(ContextKeys.OutputFormat, out var outputFormat);
        return new DeliverOutputContext(repos[0], repo, pipeline, outputFormat);
    }
}
