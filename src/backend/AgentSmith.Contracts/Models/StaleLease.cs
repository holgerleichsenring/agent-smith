using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// A lease whose heartbeat has gone stale — a CANDIDATE for the reaper, not a
/// confirmed-dead run. The reaper passes it to the liveness probe (JobId is the
/// orchestrator handle) and only releases when the probe returns positive
/// evidence the container/pod is gone.
/// </summary>
public sealed record StaleLease(string Project, TicketId TicketId, string? RunId, string? JobId);
