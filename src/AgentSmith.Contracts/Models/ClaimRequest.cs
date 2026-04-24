using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// Input to TicketClaimService.ClaimAsync. Built by webhook handlers and (future) pollers.
/// </summary>
public sealed record ClaimRequest(
    string Platform,
    string ProjectName,
    TicketId TicketId,
    string PipelineName,
    Dictionary<string, object>? InitialContext = null);
