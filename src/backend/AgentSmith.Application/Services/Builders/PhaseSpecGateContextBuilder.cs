using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>p0315d: builds the PhaseSpecGate context from the fetched ticket.</summary>
public sealed class PhaseSpecGateContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var ticket = pipeline.Get<Ticket>(ContextKeys.Ticket);
        return new PhaseSpecGateContext(ticket, pipeline);
    }
}
