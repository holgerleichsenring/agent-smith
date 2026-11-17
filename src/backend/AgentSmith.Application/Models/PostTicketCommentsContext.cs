using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-17-e061c: context for the PostTicketComments step — the run's project, the tickets'.</summary>
public sealed record PostTicketCommentsContext(PipelineContext Pipeline, ResolvedProject Project) : ICommandContext;
