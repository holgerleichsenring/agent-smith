using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Runs git commit + push inside a per-repo sandbox where /work is the repo
/// root (p0158e). Replaces LibGit2Sharp-based git ops in the handler chain.
/// Caller passes the right per-repo sandbox; the working directory is always
/// /work inside that sandbox.
/// </summary>
public sealed class SandboxGitOperations(ILogger<SandboxGitOperations> logger)
{
    private const int GitTimeoutSeconds = 120;
    private const string CredHelper =
        "credential.helper=!f() { echo \"username=x-access-token\"; echo \"password=$GIT_TOKEN\"; }; f";

    // p0192: CommitAndPRHandler now scans the staged diff between stage and
    // commit. The split methods expose each step; the original
    // CommitAndPushAsync stays as the unscanned helper for InitCommit /
    // PersistWorkBranch which write operator-controlled scaffolding and
    // don't need the agent-credential gate.
    public async Task CommitAndPushAsync(
        ISandbox sandbox, string branchName, string message,
        RepoType repoType, CancellationToken cancellationToken)
    {
        await StageAllAsync(sandbox, cancellationToken);
        await CommitAndPushStagedAsync(sandbox, branchName, message, repoType, cancellationToken);
    }

    public async Task StageAllAsync(ISandbox sandbox, CancellationToken cancellationToken)
    {
        await ConfigureUserAsync(sandbox, cancellationToken);
        await Run(sandbox, "git", new[] { "add", "-A" }, cancellationToken);
    }

    public async Task<string> GetStagedDiffAsync(ISandbox sandbox, CancellationToken cancellationToken)
    {
        var result = await sandbox.RunStepAsync(
            BuildStep("git", new[] { "diff", "--cached", "--no-color" }), null, cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"git diff --cached failed (exit {result.ExitCode}): {result.ErrorMessage}");
        return result.OutputContent ?? string.Empty;
    }

    public async Task CommitAndPushStagedAsync(
        ISandbox sandbox, string branchName, string message,
        RepoType repoType, CancellationToken cancellationToken)
    {
        var committed = await CommitAsync(sandbox, message, cancellationToken);
        if (!committed)
        {
            logger.LogInformation("Working tree clean, nothing to commit");
            throw new InvalidOperationException("nothing to commit, working tree clean");
        }
        await PushAsync(sandbox, branchName, repoType, cancellationToken);
    }

    private static async Task ConfigureUserAsync(ISandbox sandbox, CancellationToken ct)
    {
        await Run(sandbox, "git", new[] { "config", "user.email", "agent-smith@noreply.local" }, ct);
        await Run(sandbox, "git", new[] { "config", "user.name", "Agent Smith" }, ct);
    }

    private async Task<bool> CommitAsync(ISandbox sandbox, string message, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(BuildStep("git", new[] { "commit", "-m", message }), null, ct);
        if (result.ExitCode == 0) return true;
        var error = result.ErrorMessage ?? string.Empty;
        if (error.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase)) return false;
        logger.LogWarning("git commit exited {Exit}: {Error}", result.ExitCode, error);
        return false;
    }

    private static async Task PushAsync(
        ISandbox sandbox, string branch, RepoType repoType, CancellationToken ct)
    {
        var token = GitTokenResolver.Resolve(repoType);
        var env = token is null
            ? null
            : (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["GIT_TOKEN"] = token };

        var result = await sandbox.RunStepAsync(
            BuildStep("git", new[] { "-c", CredHelper, "push", "--force-with-lease", "origin", $"HEAD:{branch}" }, env), null, ct);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"git push failed (exit {result.ExitCode}): {result.ErrorMessage}");
    }

    private static async Task Run(
        ISandbox sandbox, string cmd, IReadOnlyList<string> args, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(BuildStep(cmd, args), null, ct);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"{cmd} {string.Join(' ', args)} failed (exit {result.ExitCode}): {result.ErrorMessage}");
    }

    private static Step BuildStep(string cmd, IReadOnlyList<string> args,
        IReadOnlyDictionary<string, string>? env = null) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: cmd, Args: args, Env: env, TimeoutSeconds: GitTimeoutSeconds);
}
