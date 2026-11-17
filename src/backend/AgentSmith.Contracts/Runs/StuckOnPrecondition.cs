using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Runs;

/// <summary>2026-11-16-f3fbc: a ticket whose newest code attempt stopped on a precondition, with what stopped it.</summary>
public sealed record StuckOnPrecondition(string Project, string TicketId, string RunId, string EndKind, EndDetail? Detail);
