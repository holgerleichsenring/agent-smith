using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

public sealed class FetchTicketContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var ticketId = pipeline.Get<TicketId>(ContextKeys.TicketId);
        return new FetchTicketContext(ticketId, project.Tickets, pipeline);
    }
}

public sealed class CheckoutSourceContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var branch = pipeline.TryGet<string>(ContextKeys.CheckoutBranch, out var b) && !string.IsNullOrWhiteSpace(b)
            ? new BranchName(b)
            : pipeline.TryGet<TicketId>(ContextKeys.TicketId, out var ticketId)
                ? BranchName.FromTicket(ticketId!)
                : null;

        return new CheckoutSourceContext(project.Source, branch, pipeline);
    }
}

public sealed class TryCheckoutSourceContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var branch = string.IsNullOrWhiteSpace(project.Source.DefaultBranch)
            ? null
            : new BranchName(project.Source.DefaultBranch);

        return new TryCheckoutSourceContext(project.Source, branch, pipeline);
    }
}

public sealed class LoadDomainRulesContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var path = project.CodingPrinciplesPath ?? ".agentsmith/coding-principles.md";
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new LoadDomainRulesContext(path, repo, pipeline);
    }
}

public sealed class LoadContextContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new LoadContextContext(repo, pipeline);
    }
}

public sealed class LoadCodeMapContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new LoadCodeMapContext(repo, pipeline);
    }
}

public sealed class BootstrapProjectContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new BootstrapProjectContext(repo, project.Agent, project.SkillsPath, pipeline);
    }
}
