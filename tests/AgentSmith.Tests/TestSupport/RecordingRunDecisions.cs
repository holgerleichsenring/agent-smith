using AgentSmith.Contracts.Decisions;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-09-17-0e79b: the decisions a run wrote, which is the notice's second destination
/// and the only one a run with no tracker has.</summary>
internal sealed class RecordingRunDecisions : IDecisionLogger
{
    internal List<string> Decisions { get; } = [];

    public Task LogAsync(
        string? repoPath, DecisionCategory category, string decision,
        CancellationToken cancellationToken = default, string? sourceLabel = null)
    {
        Decisions.Add(decision);
        return Task.CompletedTask;
    }
}
