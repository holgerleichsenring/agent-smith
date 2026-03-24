using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

public sealed class FetchTicketContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var ticketId = pipeline.Get<TicketId>(ContextKeys.TicketId);
        return new FetchTicketContext(ticketId, project.Tickets, pipeline);
    }
}

public sealed class CheckoutSourceContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var initMode = pipeline.TryGet<bool>(ContextKeys.InitMode, out var isInit) && isInit;

        if (initMode)
            return new CheckoutSourceContext(project.Source, new BranchName("agentsmith/init"), pipeline);

        if (pipeline.TryGet<string>(ContextKeys.ScanBranch, out var scanBranch)
            && !string.IsNullOrWhiteSpace(scanBranch))
            return new CheckoutSourceContext(project.Source, new BranchName(scanBranch), pipeline);

        if (pipeline.Has(ContextKeys.ScanRepoPath))
            return new CheckoutSourceContext(project.Source, new BranchName("main"), pipeline);

        var ticketId = pipeline.Get<TicketId>(ContextKeys.TicketId);
        return new CheckoutSourceContext(project.Source, BranchName.FromTicket(ticketId), pipeline);
    }
}

public sealed class LoadDomainRulesContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var path = project.CodingPrinciplesPath ?? ".agentsmith/coding-principles.md";
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new LoadDomainRulesContext(path, repo, pipeline);
    }
}

public sealed class LoadContextContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new LoadContextContext(repo, pipeline);
    }
}

public sealed class LoadCodeMapContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new LoadCodeMapContext(repo, pipeline);
    }
}

public sealed class BootstrapProjectContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new BootstrapProjectContext(repo, project.Agent, project.SkillsPath, pipeline);
    }
}
