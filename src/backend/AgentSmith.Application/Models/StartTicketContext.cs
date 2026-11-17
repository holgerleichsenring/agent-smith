using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-17-e061b: context for the StartTicket step — the run's project, the ticket's.</summary>
public sealed record StartTicketContext(PipelineContext Pipeline, ResolvedProject Project) : ICommandContext;
