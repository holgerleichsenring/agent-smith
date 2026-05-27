using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// Result of parsing user input into a ticket reference and project name.
/// </summary>
public sealed record ParsedIntent(TicketId TicketId, ProjectName ProjectName);
