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
        if (!pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
                ContextKeys.Sandboxes, out var sandboxes) || sandboxes is null)
            return RecordAndFail(pipeline, PersistFailureKind.Unknown,
                "PersistWorkBranch: no Sandboxes in pipeline context");

        var commitMessage = BuildCommitMessage(pipeline);
        var branch = primaryRepo.CurrentBranch.Value;

        var outcomes = new List<PerRepoPersistResult>(context.Configs.Count);
        foreach (var repo in context.Configs)
        {
            if (!sandboxes.TryGetValue(repo.Name, out var sandbox))
            {
                outcomes.Add(new PerRepoPersistResult(
                    repo.Name, PersistFailureKind.Unknown, "no sandbox available"));
                continue;
            }
            outcomes.Add(await PersistOneAsync(sandbox, repo, branch, commitMessage, cancellationToken));
        }

        return Aggregate(pipeline, outcomes);
    }

    private async Task<PerRepoPersistResult> PersistOneAsync(
        ISandbox sandbox, RepoConnection repo, string branch, string commitMessage, CancellationToken ct)
    {
        try
        {
            await gitOps.CommitAndPushAsync(sandbox, branch, commitMessage, repo.Type, ct);
            logger.LogInformation("{Repo}: pushed WIP commit on branch {Branch}", repo.Name, branch);
            return new PerRepoPersistResult(repo.Name, Kind: null, Message: null);
        }
        catch (Exception ex) when (LooksLikeEmptyCommit(ex))
        {
            logger.LogInformation("{Repo}: working tree clean — nothing to persist", repo.Name);
            return new PerRepoPersistResult(repo.Name, PersistFailureKind.NoChanges, "No local changes");
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

    private static CommandResult Aggregate(
        PipelineContext pipeline, IReadOnlyList<PerRepoPersistResult> outcomes)
    {
        var allClean = outcomes.All(o => o.Kind == PersistFailureKind.NoChanges);
        if (allClean)
            return RecordAndFail(pipeline, PersistFailureKind.NoChanges,
                "No local changes to persist in any repo");

        var worstFailure = outcomes
            .Where(o => o.Kind is not null and not PersistFailureKind.NoChanges)
            .Select(o => o.Kind!.Value)
            .DefaultIfEmpty()
            .Max();
        if (worstFailure == default)
            return CommandResult.Ok($"Persisted WIP across {outcomes.Count} repo(s)");

        var failedNames = string.Join(", ", outcomes
            .Where(o => o.Kind is not null and not PersistFailureKind.NoChanges)
            .Select(o => o.RepoName));
        return RecordAndFail(pipeline, worstFailure,
            $"Persist failed in {failedNames} ({worstFailure})");
    }

    private static CommandResult RecordAndFail(
        PipelineContext pipeline, PersistFailureKind kind, string message)
    {
        pipeline.Set(ContextKeys.PersistFailureKind, kind);
        return CommandResult.Fail(message);
    }

    private static bool LooksLikeEmptyCommit(Exception ex) =>
        ex.GetType().Name.Contains("EmptyCommit", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("no changes", StringComparison.OrdinalIgnoreCase);

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
