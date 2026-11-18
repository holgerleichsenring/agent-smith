using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>2026-11-18-d6d2b: the admin assistant's record reads lent to the ticket steward, scoped to its ticket — a run's
/// events (steps, commands and their output tails, verdicts) for the ticket's own runs only, and its project's
/// configuration. The server supplies them on a worked bound turn; a composition without them gives none.</summary>
public interface ITicketRecordReads
{
    IList<AITool> Tools();
}
