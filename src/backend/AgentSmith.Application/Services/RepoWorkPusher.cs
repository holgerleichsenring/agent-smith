using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0437: commits and pushes ONE repository's working changes onto the run branch —
/// staging, the secret scan that refuses a leaking diff, and the push itself.
/// <para>
/// Extracted from <see cref="RunWorkCheckpointer"/>, which decides WHEN work should reach
/// the branch. Putting one repo's work there is a different job, and it is the one both
/// callers share: the opportunistic checkpoint and the explicit pre-gate commit.
/// </para>
/// </summary>
public sealed class RepoWorkPusher(
    SandboxGitOperations gitOps,
    ISecretPatternScanner secretScanner,
    SandboxTargets sandboxTargets,
    ILogger<RepoWorkPusher> logger)
{
    public async Task PushAsync(
        PipelineContext pipeline, RepoConnection repo, string branch, string runId, CancellationToken ct)
    {
        var matches = sandboxTargets.SandboxesForRepo(pipeline, repo);
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
            sandbox, branch, RunCheckpointCommit.MessageFor(runId), repo.Type, ct);
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
}
