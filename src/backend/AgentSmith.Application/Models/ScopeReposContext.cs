using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0331: context for the ScopeRepos step (post-FetchTicket, pre-CheckoutSource).
/// Ticket is null on ticketless runs — the handler then only builds the remote
/// context inventory and skips classification.
/// </summary>
public sealed record ScopeReposContext(
    Ticket? Ticket,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
