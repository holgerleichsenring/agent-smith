using AgentSmith.Contracts.Runs;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-22-6d85c: the tickets whose newest code attempt — queued reservations ignored, as
/// <see cref="IPreviousAttemptReader"/> reads it — was cancelled because its process was lost
/// (<see cref="OwedWorkCancel"/>) at or after <c>since</c>, with no Retry pending on it and not itself agent-smith's
/// restart of such an end; closed and landed tickets are left out.
/// </summary>
public interface IInterruptedAttemptReader
{
    Task<IReadOnlyList<InterruptedAttempt>> ListAsync(DateTimeOffset since, CancellationToken cancellationToken);
}
