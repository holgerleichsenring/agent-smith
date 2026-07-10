using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for fetching a ticket from an external provider.
/// p0322a: TicketId is null on ticketless runs (CLI-triggered init-project) —
/// the handler skips the fetch cleanly instead of failing the step.
/// </summary>
public sealed record FetchTicketContext(
    TicketId? TicketId,
    TrackerConnection Config,
    PipelineContext Pipeline) : ICommandContext;
