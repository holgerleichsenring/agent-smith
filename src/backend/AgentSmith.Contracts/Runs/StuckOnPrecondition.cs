using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Runs;

/// <summary>2026-11-16-f3fbc: a ticket whose newest code attempt stopped on a precondition, with what stopped it and
/// when the attempt ended — a release needs evidence newer than that.</summary>
public sealed record StuckOnPrecondition(
    string Project, string TicketId, string RunId, string EndKind, EndDetail? Detail, DateTimeOffset FinishedAt);
