using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// 2026-11-18-550aa: the ticket a finished run finalizes — its project (by name, where an unmoved ticket is
/// recorded), its tracker connection and its id.
/// </summary>
public sealed record TicketFinalizeTarget(string Project, TrackerConnection Tracker, TicketId TicketId);
