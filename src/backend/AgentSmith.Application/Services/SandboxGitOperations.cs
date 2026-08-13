using AgentSmith.Application.Services.Sandbox;
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
public sealed class SandboxGitOperations(
    ILogger<SandboxGitOperations> logger, ISandboxFileReaderFactory readerFactory,
    SandboxGitIdentity identity)
{
    private const int GitTimeoutSeconds = 120;
    // p0299: untracked path (inside .git/, never staged by `git add -A`) used to hand a
    // secondary sandbox's staged diff to `git apply` in the primary sandbox.
    private const string ConsolidatePatchPath = ".git/agentsmith-consolidate.patch";
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
        await identity.EnsureConfiguredAsync(sandbox, cancellationToken);
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

    // p0399: remove files from the working tree AND the index in one step — a spec
    // revision fully replaces its directory, so what is absent from the current cut
    // leaves the tree and the staging area together, in the same revision commit.
    // --ignore-unmatch keeps an already-gone path from failing the whole revision.
    public async Task RemoveAsync(
        ISandbox sandbox, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        if (paths.Count == 0) return;
        await Run(sandbox, "git", ["rm", "-f", "--ignore-unmatch", "--", .. paths], cancellationToken);
    }

    // p0390: stage ONE path and nothing else. The work spec is committed as its own
    // commit BEFORE any source edit, so a reviewer sees the contract on its own in the
    // PR's first diff; StageAllAsync would fold whatever else the run has touched into
    // that commit. Configures the identity first — ForceStageAsync alone leaves the
    // subsequent `git commit` without a user and it fails.
    public async Task StagePathAsync(ISandbox sandbox, string path, CancellationToken cancellationToken)
    {
        await identity.EnsureConfiguredAsync(sandbox, cancellationToken);
        await ForceStageAsync(sandbox, path, cancellationToken);
    }

    // p0390: the sha of the last commit that touched a path. The work-spec writer
    // compares it against the pointer this system recorded: a DIFFERENT sha means a
    // human edited the spec on the branch, and that edit is INPUT to the next revision,
    // never something to overwrite. Empty when the path has no commit yet.
    public async Task<string> GetLastCommitForPathAsync(
        ISandbox sandbox, string path, CancellationToken cancellationToken)
    {
        var result = await sandbox.RunStepAsync(
            BuildStep("git", new[] { "log", "-1", "--format=%H", "--", path }), null, cancellationToken);
        if (result.ExitCode != 0) return string.Empty;
        return (result.OutputContent ?? string.Empty).Trim();
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

    // p0400: baseline mode for a ships_code:false phase — park the phase's working
    // changes so the verify command can run against the PRE-PHASE tree ("not worse
    // than before"). Returns true only when a stash entry was actually created;
    // "No local changes to save" exits 0 and must NOT be followed by a pop.
    public async Task<bool> StashWorkingChangesAsync(ISandbox sandbox, CancellationToken cancellationToken)
    {
        // Stash writes commit objects, so it needs the same identity a commit does.
        await identity.EnsureConfiguredAsync(sandbox, cancellationToken);
        var result = await sandbox.RunStepAsync(
            BuildStep("git", new[] { "stash", "push", "--include-untracked" }), null, cancellationToken);
        if (result.ExitCode != 0) return false;
        return !(result.OutputContent ?? string.Empty)
            .Contains("No local changes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Restores what <see cref="StashWorkingChangesAsync"/> parked. Throws when the pop fails — losing the phase's work must be loud.</summary>
    public Task RestoreStashedChangesAsync(ISandbox sandbox, CancellationToken cancellationToken) =>
        Run(sandbox, "git", new[] { "stash", "pop" }, cancellationToken);

    public async Task<string> GetStagedDiffAsync(ISandbox sandbox, CancellationToken cancellationToken)
    {
        var result = await sandbox.RunStepAsync(
            BuildStep("git", new[] { "diff", "--cached", "--no-color" }), null, cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"git diff --cached failed (exit {result.ExitCode}): {result.ErrorMessage}");
        return result.OutputContent ?? string.Empty;
    }

    // p0299: apply a unified diff (a staged diff pulled from ANOTHER sandbox of the same
    // repo) into THIS sandbox's working tree, so a mixed-stack monorepo whose per-toolchain
    // clones each carry their own edits consolidate into one commit. Best-effort: a failed
    // apply is logged, not thrown, so one bad/overlapping hunk doesn't lose the primary
    // sandbox's own changes.
    public async Task<bool> ApplyPatchFileAsync(
        ISandbox sandbox, string patchPath, CancellationToken cancellationToken)
    {
        var result = await sandbox.RunStepAsync(
            BuildStep("git", new[] { "apply", "--whitespace=nowarn", patchPath }), null, cancellationToken);
        if (result.ExitCode == 0) return true;
        logger.LogWarning(
            "git apply {Path} failed (exit {Exit}): {Error}", patchPath, result.ExitCode, result.ErrorMessage);
        return false;
    }

    // p0299: fold every NON-primary sandbox's staged diff into the primary sandbox's
    // working tree (write the diff to an untracked file in the primary, then `git apply`),
    // so a mixed-stack monorepo — one independent clone per toolchain sandbox — commits the
    // union of all its edits instead of only matches[0]. Returns how many were consolidated.
    // Single-sandbox repos take the early return and behave exactly as before.
    public async Task<int> ConsolidateSecondarySandboxesAsync(
        IReadOnlyList<KeyValuePair<string, ISandbox>> matches, ISandbox primary, CancellationToken ct)
    {
        if (matches.Count <= 1) return 0;
        var consolidated = 0;
        for (var i = 1; i < matches.Count; i++)
        {
            var secondary = matches[i].Value;
            await StageAllAsync(secondary, ct);
            var diff = await GetStagedDiffAsync(secondary, ct);
            if (string.IsNullOrWhiteSpace(diff)) continue;
            await readerFactory.Create(primary).WriteAsync(ConsolidatePatchPath, diff, ct);
            if (await ApplyPatchFileAsync(primary, ConsolidatePatchPath, ct))
                consolidated++;
        }
        return consolidated;
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

    // p0256: when the record carrier stages NOTHING even after force-staging the
    // run record, dump exactly what git sees — porcelain (incl. ignored), the
    // .agentsmith listing, cwd and the git toplevel — so the next real run pins
    // WHY the record never staged (gitignore / path / toplevel-vs-cwd mismatch),
    // instead of leaving a spent run with no PR as a silent skip. Diagnostic only.
    public async Task<string> DescribeRunRecordStateAsync(ISandbox sandbox, CancellationToken cancellationToken)
    {
        var status = await sandbox.RunStepAsync(
            BuildStep("git", new[] { "status", "--porcelain", "--ignored" }), null, cancellationToken);
        var probe = await sandbox.RunStepAsync(
            BuildStep("sh", new[] { "-c",
                "echo cwd=$(pwd); echo toplevel=$(git rev-parse --show-toplevel 2>&1); " +
                "ls -la .agentsmith 2>&1; ls -la .agentsmith/runs 2>&1" }),
            null, cancellationToken);
        return $"[git status --porcelain --ignored]\n{status.OutputContent}\n"
            + $"[probe]\n{probe.OutputContent}";
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
        // p0394: the commit primitive owns the identity guarantee. The spec-set
        // writer commits in a fresh checkout sandbox before any staging method
        // that configures the user has run — "Author identity unknown" (exit
        // 128) on a live run. p0411: checkout establishes the identity first, so
        // this is the fallback for sandboxes created outside that seam; the probe
        // in SandboxGitIdentity makes the repeat a no-op instead of a re-write.
        await identity.EnsureConfiguredAsync(sandbox, cancellationToken);
        var committed = await CommitAsync(sandbox, message, cancellationToken);
        if (!committed)
        {
            logger.LogInformation("Working tree clean, nothing to commit");
            throw new InvalidOperationException("nothing to commit, working tree clean");
        }
        // p0326: a Local repo without an 'origin' remote (the demo's materialized
        // workspace) is record-only — the local commit IS the result; a push would
        // only fail. Gated to Local so every remote-typed repo still pushes.
        if (repoType == RepoType.Local && !await HasOriginRemoteAsync(sandbox, cancellationToken))
        {
            logger.LogInformation(
                "Local repo has no 'origin' remote — commit recorded locally, push skipped (record-only)");
            return;
        }
        await PushAsync(sandbox, branchName, repoType, cancellationToken);
    }

    // p0326: `git remote` lists configured remotes, one per line; empty output on a
    // freshly `git init`ed workspace. Deterministic, locale-independent.
    private static async Task<bool> HasOriginRemoteAsync(ISandbox sandbox, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(BuildStep("git", new[] { "remote" }), null, ct);
        if (result.ExitCode != 0) return false;
        return (result.OutputContent ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains("origin", StringComparer.Ordinal);
    }

    // p0322c: false means git itself said the tree is clean — its canonical
    // "nothing to commit" phrase goes to STDOUT (OutputContent), which the old
    // check never read (it matched ErrorMessage only), so EVERY non-zero exit —
    // failing hook, bad config, perms — collapsed into false and was rethrown
    // upstream as a hardcoded clean-tree error. Silent-degradation class
    // (p0300b): any non-zero exit that is NOT the canonical phrase now fails
    // with the real git output; no pattern-guessing beyond git's own wording.
    private async Task<bool> CommitAsync(ISandbox sandbox, string message, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(BuildStep("git", new[] { "commit", "-m", message }), null, ct);
        if (result.ExitCode == 0) return true;
        var output = string.Join('\n',
            new[] { result.OutputContent, result.ErrorMessage }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (output.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase)) return false;
        logger.LogWarning("git commit exited {Exit}: {Output}", result.ExitCode, output);
        throw new InvalidOperationException($"git commit failed (exit {result.ExitCode}): {output}");
    }

    // p0360: push the current HEAD to the run branch without committing — used at PR
    // time for a repo whose work was already committed by mid-run checkpoints (clean
    // tree, branch ahead). A no-op when the remote is already up to date.
    public Task PushHeadAsync(
        ISandbox sandbox, string branch, RepoType repoType, CancellationToken cancellationToken) =>
        PushAsync(sandbox, branch, repoType, cancellationToken);

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
