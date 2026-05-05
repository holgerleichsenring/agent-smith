using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Runs git commit + push inside the sandbox so commits capture the actual
/// modified files in /work (the agentic write_file tool writes there, not to
/// the server-pod's local filesystem). Replaces LibGit2Sharp-based git ops in
/// the handler chain.
/// </summary>
public sealed class SandboxGitOperations(ILogger<SandboxGitOperations> logger)
{
    private const int GitTimeoutSeconds = 120;
    private const string CredHelper =
        "credential.helper=!f() { echo \"username=x-access-token\"; echo \"password=$GIT_TOKEN\"; }; f";

    public async Task CommitAndPushAsync(
        ISandbox sandbox, string branchName, string message, CancellationToken cancellationToken)
    {
        await ConfigureUserAsync(sandbox, cancellationToken);
        await StageAllAsync(sandbox, cancellationToken);
        var committed = await CommitAsync(sandbox, message, cancellationToken);
        if (!committed)
        {
            logger.LogInformation("Working tree clean, nothing to commit");
            throw new InvalidOperationException("nothing to commit, working tree clean");
        }
        await PushAsync(sandbox, branchName, cancellationToken);
    }

    private static async Task ConfigureUserAsync(ISandbox sandbox, CancellationToken ct)
    {
        await Run(sandbox, "git", new[] { "config", "user.email", "agent-smith@noreply.local" }, ct);
        await Run(sandbox, "git", new[] { "config", "user.name", "Agent Smith" }, ct);
    }

    private static async Task StageAllAsync(ISandbox sandbox, CancellationToken ct) =>
        await Run(sandbox, "git", new[] { "add", "-A" }, ct);

    private async Task<bool> CommitAsync(ISandbox sandbox, string message, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(BuildStep("git", new[] { "commit", "-m", message }), null, ct);
        if (result.ExitCode == 0) return true;
        var error = result.ErrorMessage ?? string.Empty;
        if (error.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase)) return false;
        logger.LogWarning("git commit exited {Exit}: {Error}", result.ExitCode, error);
        return false;
    }

    private static async Task PushAsync(ISandbox sandbox, string branch, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(
            BuildStep("git", new[] { "-c", CredHelper, "push", "--force-with-lease", "origin", $"HEAD:{branch}" }), null, ct);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"git push failed (exit {result.ExitCode}): {result.ErrorMessage}");
    }

    private static async Task Run(ISandbox sandbox, string cmd, IReadOnlyList<string> args, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(BuildStep(cmd, args), null, ct);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"{cmd} {string.Join(' ', args)} failed (exit {result.ExitCode}): {result.ErrorMessage}");
    }

    private static Step BuildStep(string cmd, IReadOnlyList<string> args) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: cmd, Args: args, TimeoutSeconds: GitTimeoutSeconds);
}
