using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Lifecycle;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Repository = AgentSmith.Domain.Entities.Repository;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Pushes the work branch when the pipeline failed mid-run after local changes
/// exist. Stamps the failure kind into <see cref="ContextKeys.PersistFailureKind"/>
/// on the pipeline context so the executor's wrapper can route logs accordingly.
/// Runs git operations in the sandbox where the modifications live.
/// </summary>
public sealed class PersistWorkBranchHandler(
    SandboxGitOperations gitOps,
    ILogger<PersistWorkBranchHandler> logger) : ICommandHandler<PersistWorkBranchContext>
{
    public async Task<CommandResult> ExecuteAsync(
        PersistWorkBranchContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<Repository>(ContextKeys.Repository, out var repo) || repo is null)
            return RecordAndFail(pipeline, PersistFailureKind.Unknown,
                "PersistWorkBranch: no Repository in pipeline context");
        if (!pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var sandbox) || sandbox is null)
            return RecordAndFail(pipeline, PersistFailureKind.Unknown,
                "PersistWorkBranch: no Sandbox in pipeline context");

        var commitMessage = BuildCommitMessage(pipeline);

        try
        {
            await gitOps.CommitAndPushAsync(sandbox, repo.CurrentBranch.Value, commitMessage, cancellationToken);
            logger.LogInformation(
                "PersistWorkBranch: pushed WIP commit on branch {Branch}", repo.CurrentBranch);
            return CommandResult.Ok($"Persisted WIP branch {repo.CurrentBranch}");
        }
        catch (Exception ex) when (LooksLikeEmptyCommit(ex))
        {
            logger.LogInformation("PersistWorkBranch: working tree clean — nothing to persist");
            return RecordAndFail(pipeline, PersistFailureKind.NoChanges,
                "No local changes to persist");
        }
        catch (Exception ex) when (LooksLikeAuth(ex))
        {
            logger.LogError(ex, "PersistWorkBranch: auth denied on push");
            return RecordAndFail(pipeline, PersistFailureKind.AuthDenied,
                $"Persist failed (auth denied): {ex.Message}", ex);
        }
        catch (Exception ex) when (LooksLikeDivergent(ex))
        {
            logger.LogError(ex, "PersistWorkBranch: remote diverged — refusing to force-push");
            return RecordAndFail(pipeline, PersistFailureKind.RemoteDivergent,
                $"Persist failed (remote diverged, no force-push): {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "PersistWorkBranch: transient network failure");
            return RecordAndFail(pipeline, PersistFailureKind.NetworkBlip,
                $"Persist failed (network): {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PersistWorkBranch: unexpected failure");
            return RecordAndFail(pipeline, PersistFailureKind.Unknown,
                $"Persist failed: {ex.Message}", ex);
        }
    }

    private static CommandResult RecordAndFail(
        PipelineContext pipeline, PersistFailureKind kind, string message, Exception? exception = null)
    {
        pipeline.Set(ContextKeys.PersistFailureKind, kind);
        return CommandResult.Fail(message, exception);
    }

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
}
