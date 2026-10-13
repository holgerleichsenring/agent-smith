using AgentSmith.Server.Services.Events;

namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59a: the question a needs-you ticket waits on, and the run that asks it.</summary>
public sealed record TicketPendingQuestion(string RunId, PendingQuestionInfo Question);
