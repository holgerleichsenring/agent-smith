using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>2026-11-17-e061c: builder for the AmendTicket step — the run's project with the run's repositories.</summary>
public sealed class AmendTicketContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline) =>
        new AmendTicketContext(pipeline, RunProject.Of(project, pipeline));
}
