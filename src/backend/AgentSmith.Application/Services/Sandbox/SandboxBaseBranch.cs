using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// The branch a clone will merge into — the repository's own answer, read from
/// <c>origin/HEAD</c>.
/// <para>
/// p0496 extracted this from <see cref="DeliveryDiff"/> so the delivery account and the
/// work-branch merge ask the same question of the same place. Nothing earlier in a run
/// carries the base: <c>RepoConnection.DefaultBranch</c> is an optional override that is
/// usually null, and <c>ISourceProvider</c> cannot be asked. The clone knows, so the
/// clone is asked.
/// </para>
/// </summary>
public sealed class SandboxBaseBranch(ILogger<SandboxBaseBranch> logger)
{
    private const int TimeoutSeconds = 120;

    /// <summary>
    /// The short base branch name without the <c>origin/</c> prefix, or null when the
    /// sandbox's clone says nothing about a base.
    /// </summary>
    public async Task<string?> ResolveAsync(ISandbox sandbox, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        var result = await sandbox.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                Command: "git", Args: ["symbolic-ref", "--short", "refs/remotes/origin/HEAD"],
                WorkingDirectory: Repository.SandboxWorkPath, TimeoutSeconds: TimeoutSeconds),
            progress: null, cancellationToken);
        if (result.ExitCode != 0)
        {
            logger.LogDebug("origin/HEAD is unreadable in sandbox {JobId} — this clone names no base", sandbox.JobId);
            return null;
        }
        var head = (result.OutputContent ?? string.Empty).Trim();
        return head.StartsWith("origin/", StringComparison.Ordinal) ? head["origin/".Length..] : null;
    }
}
