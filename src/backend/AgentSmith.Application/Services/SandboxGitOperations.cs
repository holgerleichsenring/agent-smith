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

    // p0234: force-stage a path even if it's .gitignored. The run-record under
    // .agentsmith/runs/ must ALWAYS be committable so every repo gets a PR;
    // some target repos .gitignore .agentsmith, which a plain `git add -A`
    // silently skips. Best-effort: a missing path just no-ops.
    public async Task ForceStageAsync(ISandbox sandbox, string path, CancellationToken cancellationToken)
    {
        await Run(sandbox, "git", new[] { "add", "-f", path }, cancellationToken);
    }

    // p0202: deterministic "is anything staged?" check used by
    // PersistWorkBranch to route a clean repo to NothingToCommit BEFORE
    // attempting a commit. `git diff --cached --quiet` exits 0 when nothing is
    // staged and 1 when staged changes exist — locale- and git-version-
    // independent, unlike matching git's "nothing to commit" stderr string.
    public async Task<bool> HasStagedChangesAsync(ISandbox sandbox, CancellationToken cancellationToken)
    {
        var result = await sandbox.RunStepAsync(
            BuildStep("git", new[] { "diff", "--cached", "--quiet" }), null, cancellationToken);
        return result.ExitCode != 0;
    }

    // p0226: cheap pre-check for PersistWorkBranch — does the working tree have
    // ANY change (staged or unstaged), without first running git config/add?
    // Returns true ONLY on a clean exit with non-empty porcelain output, so a
    // repo with no changes — OR a sandbox the step can't even run in (the
    // untouched repos in a multi-repo run, which return the -1 sentinel) — both
    // route to "nothing to persist" instead of hard-failing on the first
    // `git config`.
    public async Task<bool> HasWorkingChangesAsync(ISandbox sandbox, CancellationToken cancellationToken)
    {
        var result = await sandbox.RunStepAsync(
            BuildStep("git", new[] { "status", "--porcelain" }), null, cancellationToken);
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.OutputContent);
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

    // p0235: the staged file paths, used to tell a real code change apart from a
    // run-record-only stage (.agentsmith/...). A repo with only the run record
    // doesn't warrant its own PR unless it's the sole record carrier.
    public async Task<IReadOnlyList<string>> GetStagedFileNamesAsync(
        ISandbox sandbox, CancellationToken cancellationToken)
    {
        var result = await sandbox.RunStepAsync(
            BuildStep("git", new[] { "diff", "--cached", "--name-only" }), null, cancellationToken);
        if (result.ExitCode != 0) return [];
        return (result.OutputContent ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    // p0240: the repo HEAD commit SHA, folded into the ProjectMap cache key so a
    // source-only commit (which leaves dependency manifests untouched — the
    // common bug-fix case) invalidates a stale cached map instead of serving it.
    // Returns empty when HEAD can't be resolved (detached/empty repo); the caller
    // then degrades to a manifest-only key rather than failing the analyze step.
    public async Task<string> GetHeadCommitAsync(ISandbox sandbox, CancellationToken cancellationToken)
    {
        var result = await sandbox.RunStepAsync(
            BuildStep("git", new[] { "rev-parse", "HEAD" }), null, cancellationToken);
        if (result.ExitCode != 0) return string.Empty;
        return (result.OutputContent ?? string.Empty).Trim();
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
