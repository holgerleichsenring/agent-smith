using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0360: mid-run durability for the WORK itself. Commits + pushes each dirty repo
/// sandbox's working tree to the run branch, so a run that dies mid-flight (OOM'd
/// sandbox, wall-time cancel, crashed pod) loses at most the work since the last
/// checkpoint — not an hour of edits that existed only on the sandbox filesystem.
/// Triggered from the master's accepted <c>update_progress</c> replaces (the same
/// moment the ledger flushes, p0356), throttled by
/// <c>agent.checkpoint_push_min_interval_seconds</c>. Every push passes the same
/// secret-pattern gate as the final commit — a checkpoint must never become the
/// side door around the credential scan. Best-effort per repo: a failed push is
/// logged and retried implicitly at the next checkpoint; it never fails the run.
/// </summary>
public sealed class RunWorkCheckpointer(
    SandboxGitOperations gitOps,
    ISecretPatternScanner secretScanner,
    ILogger<RunWorkCheckpointer> logger)
{
    private DateTimeOffset _lastAttempt = DateTimeOffset.MinValue;

    public async Task CheckpointAsync(
        PipelineContext pipeline, int minIntervalSeconds, CancellationToken cancellationToken)
    {
        if (minIntervalSeconds <= 0) return;
        var now = DateTimeOffset.UtcNow;
        if (now - _lastAttempt < TimeSpan.FromSeconds(minIntervalSeconds)) return;
        _lastAttempt = now;

        if (!pipeline.TryGet<Repository>(ContextKeys.Repository, out var repository) || repository is null)
            return;
        if (!pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos)
            || repos is null || repos.Count == 0)
            return;
        var runId = pipeline.TryGet<string>(ContextKeys.RunId, out var rid) && rid is not null ? rid : "unknown";
        var branch = repository.CurrentBranch.Value;

        foreach (var repo in repos)
        {
            try
            {
                await CheckpointRepoAsync(pipeline, repo, branch, runId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return; // the run is being cancelled — the salvage paths own persistence now
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "{Repo}: checkpoint push failed — work stays local until the next checkpoint",
                    repo.Name);
            }
        }
    }

    private async Task CheckpointRepoAsync(
        PipelineContext pipeline, RepoConnection repo, string branch, string runId, CancellationToken ct)
    {
        var matches = SandboxTargets.SandboxesForRepo(pipeline, repo);
        if (matches.Count == 0) return;
        // Multi-context monorepo: checkpoint the first sandbox, same convention as
        // PersistWorkBranch; secondary-sandbox edits consolidate at CommitAndPR time.
        var sandbox = matches[0].Value;

        if (!await gitOps.HasWorkingChangesAsync(sandbox, ct)) return;
        await gitOps.StageAllAsync(sandbox, ct);
        if (!await gitOps.HasStagedChangesAsync(sandbox, ct)) return;

        var diff = await gitOps.GetStagedDiffAsync(sandbox, ct);
        var leaks = secretScanner.Scan($"{repo.Name}-checkpoint-diff", diff);
        if (leaks.Count > 0)
        {
            logger.LogError(
                "{Repo}: secret-pattern match in checkpoint diff at line {Line} ({Pattern}) — checkpoint NOT pushed",
                repo.Name, leaks[0].Line, leaks[0].Pattern);
            return;
        }

        var staged = await gitOps.GetStagedFileNamesAsync(sandbox, ct);
        await gitOps.CommitAndPushStagedAsync(
            sandbox, branch, $"[checkpoint] agent-smith run {runId}", repo.Type, ct);
        MarkCheckpointed(pipeline, repo.Name,
            staged.Any(n => !RunRecordPaths.IsRunRecordPath(n)));
        logger.LogInformation(
            "{Repo}: checkpoint pushed to {Branch} ({Files} file(s))", repo.Name, branch, staged.Count);
    }

    // Repo name → "some checkpoint carried real code" (OR-accumulated across
    // checkpoints). CommitAndPRHandler reads this so a clean-tree-at-PR-time repo
    // still counts as changed and opens its PR.
    private static void MarkCheckpointed(PipelineContext pipeline, string repoName, bool hasCode)
    {
        var map = pipeline.TryGet<Dictionary<string, bool>>(ContextKeys.CheckpointedRepos, out var existing)
            && existing is not null
            ? existing
            : new Dictionary<string, bool>(StringComparer.Ordinal);
        map[repoName] = hasCode || (map.TryGetValue(repoName, out var prior) && prior);
        pipeline.Set(ContextKeys.CheckpointedRepos, map);
    }

    /// <summary>True when a checkpoint with real (non-run-record) code was pushed for the repo.</summary>
    public static bool HasCheckpointedCode(PipelineContext pipeline, string repoName) =>
        pipeline.TryGet<Dictionary<string, bool>>(ContextKeys.CheckpointedRepos, out var map)
        && map is not null && map.TryGetValue(repoName, out var hasCode) && hasCode;

    /// <summary>True when ANY checkpoint (code or record-only) was pushed for the repo.</summary>
    public static bool WasCheckpointed(PipelineContext pipeline, string repoName) =>
        pipeline.TryGet<Dictionary<string, bool>>(ContextKeys.CheckpointedRepos, out var map)
        && map is not null && map.ContainsKey(repoName);
}
