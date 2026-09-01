using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-01-b467: where a run began, read out of the branch's own history.
/// <para>
/// A run puts its work on the branch as checkpoint commits carrying its run id
/// (<see cref="RunCheckpointCommit"/>). The OLDEST of those is the first thing this run
/// added, so its first parent is the branch as the run found it. Asking for the run's own
/// marker is what makes the answer safe: a commit somebody else made on the same branch
/// cannot be mistaken for the run's starting point, and a branch this run has not committed
/// to yet says so instead of guessing.
/// </para>
/// </summary>
public sealed class SandboxRunStartCommit(ILogger<SandboxRunStartCommit> logger)
{
    private const int TimeoutSeconds = 120;

    /// <summary>
    /// A revision naming the commit the run started from, or null when the run has committed
    /// nothing on this branch (and when there is no run id to ask about).
    /// </summary>
    public async Task<string?> ResolveAsync(
        ISandbox sandbox, string? runId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        if (string.IsNullOrWhiteSpace(runId)) return null;
        var result = await sandbox.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                Command: "git",
                Args: ["log", "--format=%H", "--fixed-strings",
                    $"--grep={RunCheckpointCommit.MessageFor(runId)}", "HEAD"],
                WorkingDirectory: Repository.SandboxWorkPath, TimeoutSeconds: TimeoutSeconds),
            progress: null, cancellationToken);
        if (result.ExitCode != 0)
        {
            logger.LogDebug(
                "The history of sandbox {JobId} is unreadable — it cannot say where run {RunId} began",
                sandbox.JobId, runId);
            return null;
        }

        var commits = (result.OutputContent ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (commits.Length == 0)
        {
            logger.LogDebug("Run {RunId} has committed nothing on this branch yet", runId);
            return null;
        }

        // git log lists newest first, so the LAST line is this run's first commit.
        return $"{commits[^1]}^";
    }
}
