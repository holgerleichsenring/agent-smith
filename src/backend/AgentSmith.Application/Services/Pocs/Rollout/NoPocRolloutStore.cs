using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Rollout;

/// <summary>2026-11-23-c81ec: without a database there is no run row and no revision to record a rollout on.</summary>
public sealed class NoPocRolloutStore : IPocRolloutStore
{
    public Task<DateTimeOffset?> RunStartedAtAsync(string runId, CancellationToken ct) => Task.FromResult<DateTimeOffset?>(null);

    public Task<bool> HasHealthyRolloutAsync(long pocId, CancellationToken ct) => Task.FromResult(false);

    public Task RecordCommitAsync(long pocId, int revision, string commitSha, CancellationToken ct) => Task.CompletedTask;

    public Task RecordOutcomeAsync(PocRolloutOutcome outcome, CancellationToken ct) => Task.CompletedTask;
}
