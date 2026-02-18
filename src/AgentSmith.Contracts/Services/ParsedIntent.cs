using AgentSmith.Domain.ValueObjects;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Result of parsing user input into a ticket reference and project name.
/// </summary>
public sealed record ParsedIntent(TicketId TicketId, ProjectName ProjectName);
