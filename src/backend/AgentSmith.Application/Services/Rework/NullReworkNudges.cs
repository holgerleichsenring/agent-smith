using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Rework;

/// <summary>2026-10-08-0781: a composition without a database (the CLI) has no rework queue.</summary>
public sealed class NullReworkNudges : IReworkNudges, IReworkWatermark
{
    public Task EnqueueAsync(ReworkNudgeRequest request, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task WithholdRunAsync(string runId, DateTimeOffset at, CancellationToken cancellationToken) => Task.CompletedTask;
}
