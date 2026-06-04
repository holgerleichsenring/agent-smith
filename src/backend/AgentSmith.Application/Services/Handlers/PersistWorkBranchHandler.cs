using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Lifecycle;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Pushes a WIP commit per repo when the pipeline failed mid-run after local
/// changes exist. Iterates Configs and attempts the push per-repo in its own
/// /work/{name}/ subdir; aggregates outcomes so the operator log lists each
/// repo's result. Stamps the worst-case failure kind into
/// ContextKeys.PersistFailureKind so the executor's wrapper can route logs
/// accordingly.
/// </summary>
public sealed class PersistWorkBranchHandler(
    SandboxGitOperations gitOps,
    ILogger<PersistWorkBranchHandler> logger) : ICommandHandler<PersistWorkBranchContext>
{
    public async Task<CommandResult> ExecuteAsync(
        PersistWorkBranchContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<Repository>(ContextKeys.Repository, out var primaryRepo) || primaryRepo is null)
            return RecordAndFail(pipeline, PersistFailureKind.Unknown,
                "PersistWorkBranch: no Repository in pipeline context");
        var commitMessage = BuildCommitMessage(pipeline);
        var branch = primaryRepo.CurrentBranch.Value;

        var outcomes = new List<PerRepoPersistResult>(context.Configs.Count);
        foreach (var repo in context.Configs)
        {
            var matches = SandboxTargets.SandboxesForRepo(pipeline, repo);
            if (matches.Count == 0)
            {
                outcomes.Add(new PerRepoPersistResult(
                    repo.Name, PersistFailureKind.Unknown, "no sandbox available"));
                continue;
            }
            // Multi-context monorepo: persist from the first sandbox of the repo.
            // Per-context branch aggregation is a follow-up if monorepo edits land.
            outcomes.Add(await PersistOneAsync(matches[0].Value, repo, branch, commitMessage, cancellationToken));
        }

        return Aggregate(pipeline, outcomes);
    }

    private async Task<PerRepoPersistResult> PersistOneAsync(
        ISandbox sandbox, RepoConnection repo, string branch, string commitMessage, CancellationToken ct)
    {
        try
        {
            // p0226: check for changes BEFORE git config / git add. The master
            // edits only the repos it needs (1 of N in a multi-repo run); the
            // untouched repos have nothing to persist — and their sandboxes may
            // not even be in a state to run git, returning the -1 sentinel on
            // the first `git config`. Probing with a side-effect-free
            // `git status --porcelain` first routes those to NoChanges cleanly,
            // so persist goes green when the repos that DO carry work are saved.
            if (!await gitOps.HasWorkingChangesAsync(sandbox, ct))
            {
                logger.LogInformation("{Repo}: nothing to persist", repo.Name);
                return new PerRepoPersistResult(repo.Name, PersistFailureKind.NoChanges, "No local changes");
            }

            // p0202: stage, then confirm something is staged before committing,
            // so no stray `git commit` exit-1 appears under the parent step.
            await gitOps.StageAllAsync(sandbox, ct);
            if (!await gitOps.HasStagedChangesAsync(sandbox, ct))
            {
                logger.LogInformation("{Repo}: working tree clean — nothing to persist", repo.Name);
                return new PerRepoPersistResult(repo.Name, PersistFailureKind.NoChanges, "No local changes");
            }

            await gitOps.CommitAndPushStagedAsync(sandbox, branch, commitMessage, repo.Type, ct);
            logger.LogInformation("{Repo}: pushed WIP commit on branch {Branch}", repo.Name, branch);
            return new PerRepoPersistResult(repo.Name, Kind: null, Message: null);
        }
        catch (Exception ex) when (LooksLikeAuth(ex))
        {
            logger.LogError(ex, "{Repo}: auth denied on push", repo.Name);
            return new PerRepoPersistResult(repo.Name, PersistFailureKind.AuthDenied, ex.Message);
        }
        catch (Exception ex) when (LooksLikeDivergent(ex))
        {
            logger.LogError(ex, "{Repo}: remote diverged — refusing to force-push", repo.Name);
            return new PerRepoPersistResult(repo.Name, PersistFailureKind.RemoteDivergent, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "{Repo}: transient network failure", repo.Name);
            return new PerRepoPersistResult(repo.Name, PersistFailureKind.NetworkBlip, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Repo}: unexpected persist failure", repo.Name);
            return new PerRepoPersistResult(repo.Name, PersistFailureKind.Unknown, ex.Message);
        }
    }

    // p0202: the parent step stays green when every per-repo outcome is
    // Success (pushed) OR NothingToCommit; only a real failure (auth /
    // divergent / network / unknown) flips it to red, and the failing repos
    // are named in the summary so the operator never has to expand to see
    // that something went wrong.
    private static CommandResult Aggregate(
        PipelineContext pipeline, IReadOnlyList<PerRepoPersistResult> outcomes)
    {
        var failures = outcomes
            .Where(o => o.Kind is not null and not PersistFailureKind.NoChanges)
            .ToList();
        var pushed = outcomes.Count(o => o.Kind is null);
        var nothingToCommit = outcomes.Count(o => o.Kind == PersistFailureKind.NoChanges);

        if (failures.Count == 0)
            return CommandResult.Ok(
                $"Persisted work branch across {outcomes.Count} repo(s): " +
                $"{pushed} pushed, {nothingToCommit} nothing to commit");

        var worstFailure = failures.Select(o => o.Kind!.Value).Max();
        var failedNames = string.Join(", ", failures.Select(o => o.RepoName));
        return RecordAndFail(pipeline, worstFailure,
            $"Persist failed in {failures.Count}/{outcomes.Count} repo(s): {failedNames} ({worstFailure})");
    }

    private static CommandResult RecordAndFail(
        PipelineContext pipeline, PersistFailureKind kind, string message)
    {
        pipeline.Set(ContextKeys.PersistFailureKind, kind);
        return CommandResult.Fail(message);
    }

    private static bool LooksLikeAuth(Exception ex) =>
        ex.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("401")
        || ex.Message.Contains("403")
        || ex.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeDivergent(Exception ex) =>
        ex.Message.Contains("non-fast-forward", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("rejected", StringComparison.OrdinalIgnoreCase);

    private static string BuildCommitMessage(PipelineContext pipeline)
    {
        var runId = pipeline.TryGet<string>(ContextKeys.RunId, out var rid) ? rid : "unknown";
        var pipelineName = pipeline.TryGet<ResolvedPipelineConfig>(
            ContextKeys.ResolvedPipeline, out var resolved) && resolved is not null
            ? resolved.PipelineName : "unknown";
        var failedStep = pipeline.TryGet<string>(ContextKeys.FailedStepName, out var fs) ? fs : "unknown";
        return $"[wip] agent-smith run {runId}\n\n" +
               $"Run-Id: {runId}\n" +
               $"Pipeline: {pipelineName}\n" +
               $"Failed-Step: {failedStep}\n";
    }

    private sealed record PerRepoPersistResult(
        string RepoName, PersistFailureKind? Kind, string? Message);
}
