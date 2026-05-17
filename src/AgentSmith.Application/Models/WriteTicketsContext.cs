using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for writing autonomous findings as tickets via the ticket provider.
/// </summary>
public sealed record WriteTicketsContext(
    TrackerConnection TrackerConnection,
    int MaxTickets,
    int MinConfidence,
    PipelineContext Pipeline) : ICommandContext;
