using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// 2026-10-02-5ab2b: the DB-free binding — without a run row there is no stored request to
/// claim, so every popped request starts as it always did. The server swaps in DbRunStartClaim.
/// </summary>
public sealed class NoOpRunStartClaim : IRunStartClaim
{
    public Task<RunStartClaimOutcome> ClaimAsync(string runId, CancellationToken cancellationToken) =>
        Task.FromResult(RunStartClaimOutcome.Unclaimed);
}
