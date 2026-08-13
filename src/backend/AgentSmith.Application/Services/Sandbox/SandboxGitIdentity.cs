using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0411: the committing identity of a repo sandbox — a framework-owned fact,
/// established ONCE per sandbox at checkout. Run 1b4b spent 52 `git config user.*`
/// calls on it: OUR waste, not the model's — every mid-run checkpoint (p0360) ran
/// ConfigureUser twice per repo (StageAll, then CommitAndPushStaged) and wrote an
/// identity that had never changed. Configured at checkout, the repeats become one
/// side-effect-free probe.
/// <para>
/// Non-destructive: a sandbox that already resolves an identity — a bind-mounted
/// local repo carrying the operator's own git config — keeps it. The commit path
/// calls this too, so a sandbox created outside the checkout seam still commits.
/// </para>
/// </summary>
public sealed class SandboxGitIdentity(ILogger<SandboxGitIdentity> logger)
{
    public const string Name = "Agent Smith";
    public const string Email = "agent-smith@noreply.local";
    private const int TimeoutSeconds = 60;

    /// <summary>
    /// Ensures the sandbox can commit. Returns true when THIS call set the identity,
    /// false when git already resolved one (or the probe could not run).
    /// </summary>
    public async Task<bool> EnsureConfiguredAsync(ISandbox sandbox, CancellationToken cancellationToken)
    {
        if (await HasIdentityAsync(sandbox, cancellationToken)) return false;
        await SetAsync(sandbox, "user.email", Email, cancellationToken);
        await SetAsync(sandbox, "user.name", Name, cancellationToken);
        logger.LogDebug("Configured the git identity in sandbox {JobId}", sandbox.JobId);
        return true;
    }

    // `git config --get user.email` resolves local, global and system config, so an
    // operator's own identity in a bind-mounted repo is found and left alone.
    private static async Task<bool> HasIdentityAsync(ISandbox sandbox, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(
            BuildStep(new[] { "config", "--get", "user.email" }), null, ct);
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.OutputContent);
    }

    private static async Task SetAsync(ISandbox sandbox, string key, string value, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(BuildStep(new[] { "config", key, value }), null, ct);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"git config {key} failed (exit {result.ExitCode}): {result.ErrorMessage}");
    }

    private static Step BuildStep(IReadOnlyList<string> args) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git", Args: args, TimeoutSeconds: TimeoutSeconds);
}
