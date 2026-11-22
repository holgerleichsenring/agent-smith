using AgentSmith.Server.Models;

namespace AgentSmith.Server.Contracts;

/// <summary>2026-11-22-6d85c: Retry — an unchanged attempt after any end but a success (<see cref="Services.Rework.RunRetry"/>),
/// for the callers that are not the endpoint: the interrupted restart asks it as agent-smith.</summary>
public interface IRunRetry
{
    Task<RunRetryResult> RetryAsync(string runId, string principal, string? name, RetryOrigin origin, CancellationToken ct);
}
