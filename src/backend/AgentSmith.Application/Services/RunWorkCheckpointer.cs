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
    RepoWorkPusher pusher,
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
        await PushAsync(pipeline, cancellationToken);
    }

    /// <summary>
    /// p0437: commit and push NOW, whatever the interval says.
    /// <para>
    /// A checkpoint is opportunistic by design — it fires when the progress ledger flips,
    /// at most every CheckpointPushMinIntervalSeconds. That is right for durability and
    /// wrong for a GATE: whether the phase verification can see the phase's work must not
    /// depend on whether an unrelated timer happened to fire. Measured live on ticket
    /// 19106: the master wrote its file, the gate read the branch seven seconds before the
    /// work reached it, and five satisfied criteria were reported outstanding.
    /// </para>
    /// <para>
    /// Same path, same secret scan, same staging rules — one mechanism that knows how to
    /// put work on a branch, asked directly instead of waited for.
    /// </para>
    /// </summary>
    public Task PushNowAsync(PipelineContext pipeline, CancellationToken cancellationToken)
    {
        _lastAttempt = DateTimeOffset.UtcNow;
        return PushAsync(pipeline, cancellationToken);
    }

    private async Task PushAsync(PipelineContext pipeline, CancellationToken cancellationToken)
    {
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
                await pusher.PushAsync(pipeline, repo, branch, runId, cancellationToken);
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

    public static bool HasCheckpointedCode(PipelineContext pipeline, string repoName) =>
        pipeline.TryGet<Dictionary<string, bool>>(ContextKeys.CheckpointedRepos, out var map)
        && map is not null && map.TryGetValue(repoName, out var hasCode) && hasCode;

    /// <summary>True when ANY checkpoint (code or record-only) was pushed for the repo.</summary>
    public static bool WasCheckpointed(PipelineContext pipeline, string repoName) =>
        pipeline.TryGet<Dictionary<string, bool>>(ContextKeys.CheckpointedRepos, out var map)
        && map is not null && map.ContainsKey(repoName);
}
