using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: makes one revision visible — commit + push, draft pull request at that
/// commit, pointer recorded, set published onto the run context. A failed write is
/// reported and the run CONTINUES from the in-memory set: losing the reviewer's
/// view must not lose the run.
/// </summary>
public sealed class SpecSetPublisher(
    ISpecSetWriter writer,
    ISpecSetPointerStore pointers,
    ISpecPullRequestOpener prOpener,
    SpecRefusalReporter refusals,
    Contracts.Persistence.IRunArtifactStore artifacts,
    ILogger<SpecSetPublisher> logger) : ISpecSetPublisher
{
    public async Task<CommandResult> PublishAsync(
        PipelineContext pipeline, string project, RepoConnection carryingRepo,
        SpecSet set, IReadOnlyList<IgnoredInstruction> ignoredInstructions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);
        await refusals.ReportAsync(pipeline, ignoredInstructions, cancellationToken);
        PublishToContext(pipeline, carryingRepo, set);
        await CacheForViewerAsync(pipeline, set, cancellationToken);

        var write = await writer.WriteAsync(pipeline, carryingRepo, set, cancellationToken);
        if (!write.Written)
        {
            logger.LogWarning(
                "Spec set {Key} could not be committed: {Error}", set.Key, write.Error);
            return CommandResult.Ok(
                $"Spec derived but not committed ({write.Error}) — the run continues from it in-memory");
        }
        pipeline.Set(ContextKeys.SpecRevisionSha, write.CommitSha!);
        await SavePointerAsync(project, carryingRepo, set, write.CommitSha!, cancellationToken);
        await prOpener.OpenAsync(pipeline, carryingRepo, set, cancellationToken);
        return CommandResult.Ok(Describe(set));
    }

    private static void PublishToContext(
        PipelineContext pipeline, RepoConnection carryingRepo, SpecSet set)
    {
        pipeline.Set(ContextKeys.SpecSet, set);
        pipeline.Set(ContextKeys.SpecRepo, carryingRepo.Name ?? string.Empty);
        if (set.Handback is { } handback && handback.Case != SpecHandbackCase.None)
            pipeline.Set(ContextKeys.SpecHandback, handback);
    }

    // The pointer carries the hand-back state forward unchanged here; the hand-back
    // step owns the counters, because it is the step that knows a park happened.
    private async Task SavePointerAsync(
        string project, RepoConnection carryingRepo, SpecSet set, string sha, CancellationToken ct)
    {
        var existing = await pointers.GetAsync(project, set.Key, ct);
        await pointers.SaveAsync(project, new SpecSetPointer(
            set.Key, carryingRepo.Name ?? string.Empty, sha, set.Current.Number,
            existing?.LastHandbackCase ?? SpecHandbackCase.None,
            existing?.RepeatedHandbackCount ?? 0,
            existing?.HandbackSourceSha), ct);
    }

    // The run detail reads this slot exactly as it reads plan.md. Best-effort: a
    // cold cache costs the viewer a panel, never the run.
    private async Task CacheForViewerAsync(
        PipelineContext pipeline, SpecSet set, CancellationToken ct)
    {
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return;
        try
        {
            await artifacts.WriteSpecMarkdownAsync(runId!, SpecMarkdown.Render(set), ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Caching the spec set for the run detail failed");
        }
    }

    private static string Describe(SpecSet set) =>
        set.IsHandedBack
            ? $"Spec {set.Key} handed back: {set.Handback!.Case}"
            : $"Spec {set.Key} revision {set.Current.Number} ({set.Current.Cause}): "
              + $"{set.Phases.Count} phase(s), {set.Accounting.Discarded.Count} segment(s) discarded";
}
