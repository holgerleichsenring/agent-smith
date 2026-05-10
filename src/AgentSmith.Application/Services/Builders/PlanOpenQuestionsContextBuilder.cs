using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

public sealed class PlanOpenQuestionsContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var ticket = pipeline.Get<Ticket>(ContextKeys.Ticket);
        return new PlanOpenQuestionsContext(ticket, project.Tickets, pipeline);
    }
}
