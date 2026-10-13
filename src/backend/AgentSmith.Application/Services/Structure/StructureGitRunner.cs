using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-13-2d0c: runs one git command at the repository root of a sandbox for the structure
/// readers — the base resolution, the listings and the reads at the base share one step shape.
/// </summary>
public sealed class StructureGitRunner
{
    private const int GitTimeoutSeconds = 60;

    public Task<StepResult> RunAsync(ISandbox sandbox, string[] args, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: "git", Args: args,
            WorkingDirectory: Repository.SandboxWorkPath, TimeoutSeconds: GitTimeoutSeconds);
        return sandbox.RunStepAsync(step, null, cancellationToken);
    }
}
