using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// p0330: safe default so every composition root resolves — a deployment without
/// the relational store has no persisted cancel flag to read, so the pre-start
/// gates report "not cancelled" and behave exactly as before. The Server swaps in
/// DbRunCancelStateReader. Symmetric to <see cref="NoOpActiveRunLease"/>.
/// </summary>
public sealed class NoOpRunCancelStateReader : IRunCancelStateReader
{
    public Task<bool> IsCancelRequestedAsync(string runId, CancellationToken cancellationToken)
        => Task.FromResult(false);
}
