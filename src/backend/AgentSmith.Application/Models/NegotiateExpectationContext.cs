using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0328: context for the NegotiateExpectation step (post-AnalyzeCode,
/// pre-GeneratePlan). Ticket is null on ticketless runs — the handler skips
/// cleanly then (nothing to negotiate against, no ratification transport).
/// </summary>
public sealed record NegotiateExpectationContext(
    Ticket? Ticket,
    AgentConfig AgentConfig,
    TrackerConnection? Tracker,
    PipelineContext Pipeline) : ICommandContext;
