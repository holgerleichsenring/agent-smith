using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Configuration;
using AgentSmith.Domain.ValueObjects;

namespace AgentSmith.Application.Commands.Contexts;

/// <summary>
/// Context for fetching a ticket from an external provider.
/// </summary>
public sealed record FetchTicketContext(
    TicketId TicketId,
    TicketConfig Config,
    PipelineContext Pipeline) : ICommandContext;
