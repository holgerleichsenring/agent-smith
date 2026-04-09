using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

public sealed class LoadVisionContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new LoadVisionContext(repo, pipeline);
    }
}

public sealed class LoadRunsContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var lookback = pipeline.TryGet<int>("AutonomousLookbackRuns", out var lb) ? lb : 10;
        return new LoadRunsContext(repo, lookback, pipeline);
    }
}

public sealed class WriteTicketsContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var maxTickets = pipeline.TryGet<int>("AutonomousMaxTickets", out var mt) ? mt : 3;
        var minConfidence = pipeline.TryGet<int>("AutonomousMinConfidence", out var mc) ? mc : 7;
        return new WriteTicketsContext(project.Tickets, maxTickets, minConfidence, pipeline);
    }
}
