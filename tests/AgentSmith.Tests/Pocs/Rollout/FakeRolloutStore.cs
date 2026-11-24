using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Tests.Pocs.Rollout;

/// <summary>2026-11-23-c81ec: the run's start, a revision rolled out healthy before or not, and what the follow wrote.</summary>
internal sealed class FakeRolloutStore : IPocRolloutStore
{
    public DateTimeOffset? StartedAt { get; set; }

    public bool HealthyBefore { get; set; }

    public List<PocRolloutOutcome> Outcomes { get; } = [];

    public List<string> Commits { get; } = [];

    public Task<DateTimeOffset?> RunStartedAtAsync(string runId, CancellationToken ct) => Task.FromResult(StartedAt);

    public Task<bool> HasHealthyRolloutAsync(long pocId, CancellationToken ct) => Task.FromResult(HealthyBefore);

    public Task RecordCommitAsync(long pocId, int revision, string commitSha, CancellationToken ct)
    {
        Commits.Add(commitSha);
        return Task.CompletedTask;
    }

    public Task RecordOutcomeAsync(PocRolloutOutcome outcome, CancellationToken ct)
    {
        Outcomes.Add(outcome);
        return Task.CompletedTask;
    }
}
