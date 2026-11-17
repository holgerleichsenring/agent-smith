using AgentSmith.Contracts.Runs;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-16-f3fbc: the tickets whose newest code attempt — queued reservations ignored, as
/// <see cref="IPreviousAttemptReader"/> reads it — finished Failed with an end kind in
/// <see cref="RunEndKinds.Preconditions"/>.
/// </summary>
public interface IStuckOnPreconditionReader
{
    Task<IReadOnlyList<StuckOnPrecondition>> ListAsync(CancellationToken cancellationToken);
}
