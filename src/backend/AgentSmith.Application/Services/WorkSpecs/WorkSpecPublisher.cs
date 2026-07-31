using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.WorkSpecs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: makes one revision visible — commit + push, draft PR at that commit,
/// pointer recorded, revision published onto the run context. A failed write is
/// reported and the run CONTINUES: the spec is additional context, never a gate.
/// </summary>
public sealed class WorkSpecPublisher(
    IWorkSpecWriter writer,
    IWorkSpecPointerStore pointers,
    IEarlySpecPullRequestOpener prOpener,
    WorkSpecRefusalReporter refusals,
    Contracts.Persistence.IRunArtifactStore artifacts,
    ILogger<WorkSpecPublisher> logger) : IWorkSpecPublisher
{
    public async Task<CommandResult> PublishAsync(
        PipelineContext pipeline, string project, RepoConnection carryingRepo,
        WorkSpecArtifact artifact, IReadOnlyList<IgnoredInstruction> ignoredInstructions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        await refusals.ReportAsync(pipeline, ignoredInstructions, cancellationToken);
        PublishToContext(pipeline, carryingRepo, artifact);
        await CacheForViewerAsync(pipeline, artifact, cancellationToken);

        var write = await writer.WriteAsync(pipeline, carryingRepo, artifact, cancellationToken);
        if (!write.Written)
        {
            logger.LogWarning(
                "Work spec {Key} could not be committed: {Error}", artifact.Spec.Key, write.Error);
            return CommandResult.Ok(
                $"Work spec derived but not committed ({write.Error}) — the run continues from it in-memory");
        }
        pipeline.Set(ContextKeys.WorkSpecRevisionSha, write.CommitSha!);
        await SavePointerAsync(project, carryingRepo, artifact, write.CommitSha!, cancellationToken);
        await prOpener.OpenAsync(pipeline, carryingRepo, artifact, cancellationToken);
        return CommandResult.Ok(Describe(artifact));
    }

    private static void PublishToContext(
        PipelineContext pipeline, RepoConnection carryingRepo, WorkSpecArtifact artifact)
    {
        // The master's input is THE CURRENT REVISION. There is no "ratified" spec
        // state — introducing one would smuggle a gate back in through vocabulary.
        pipeline.Set(ContextKeys.WorkSpec, artifact);
        pipeline.Set(ContextKeys.WorkSpecRepo, carryingRepo.Name ?? string.Empty);
        if (artifact.Spec.Handback is { } handback && handback.Case != WorkSpecHandbackCase.None)
            pipeline.Set(ContextKeys.WorkSpecHandback, handback);
    }

    // The pointer carries the hand-back state forward unchanged here; the hand-back
    // step owns the counters, because it is the step that knows a park happened.
    private async Task SavePointerAsync(
        string project, RepoConnection carryingRepo, WorkSpecArtifact artifact,
        string sha, CancellationToken ct)
    {
        var existing = await pointers.GetAsync(project, artifact.Spec.Key, ct);
        await pointers.SaveAsync(project, new WorkSpecPointer(
            artifact.Spec.Key, carryingRepo.Name ?? string.Empty, sha,
            artifact.Spec.Current.Number,
            existing?.LastHandbackCase ?? WorkSpecHandbackCase.None,
            existing?.RepeatedHandbackCount ?? 0,
            existing?.HandbackSourceSha), ct);
    }

    // The run detail reads this slot exactly as it reads plan.md. Best-effort: a
    // cold cache costs the viewer a panel, never the run.
    private async Task CacheForViewerAsync(
        PipelineContext pipeline, WorkSpecArtifact artifact, CancellationToken ct)
    {
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return;
        try
        {
            await artifacts.WriteSpecMarkdownAsync(runId!, WorkSpecMarkdown.Render(artifact), ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Caching the work spec for the run detail failed");
        }
    }

    private static string Describe(WorkSpecArtifact artifact) =>
        $"Work spec {artifact.Spec.Key} revision {artifact.Spec.Current.Number} "
        + $"({artifact.Spec.Current.Cause}): {artifact.Spec.Requirements.Count} requirement(s), "
        + $"{artifact.Spec.Constraints.Count} constraint(s)";
}
