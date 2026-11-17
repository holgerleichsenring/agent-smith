using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-17-e061a: context for the ReopenTicket step — the run's project, the ticket's.</summary>
public sealed record ReopenTicketContext(PipelineContext Pipeline, ResolvedProject Project) : ICommandContext;
