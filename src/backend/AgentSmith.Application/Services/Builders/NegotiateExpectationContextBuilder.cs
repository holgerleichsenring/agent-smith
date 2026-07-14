using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// p0328: NegotiateExpectation — post-AnalyzeCode, pre-GeneratePlan. Ticket is
/// optional (ticketless runs skip negotiation in the handler); the tracker
/// connection feeds the per-platform expectation comment.
/// </summary>
public sealed class NegotiateExpectationContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var ticket = pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var t) ? t : null;
        return new NegotiateExpectationContext(
            ticket, pipeline.Resolved().Agent, project.Tracker, pipeline);
    }
}
