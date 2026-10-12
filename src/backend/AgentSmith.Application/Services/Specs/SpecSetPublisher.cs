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
/// <para>
/// 2026-10-12-f5f7d: the set is PLACED first — each spec into the repository it names — and a
/// placement or a write that cannot be made (a spec naming several repositories, a repository not
/// checked out) fails the step with the name. The earliest spec's repository is the carrying one; a
/// set without specs keeps the one it had. 2026-10-12-f5f7e: every series repository gets its own
/// draft spec pull request.
/// </para>
/// <para>
/// 2026-10-12-f5f7f: a write into several repositories that failed is rolled back and FAILS the step.
/// </para>
/// </summary>
public sealed class SpecSetPublisher(
    ISpecSetWriter writer,
    SeriesRepos placer,
    SpecSetPointerRecorder pointer,
    ISpecPullRequestOpener prOpener,
    SpecRefusalReporter refusals,
    Contracts.Persistence.IRunArtifactStore artifacts,
    ILogger<SpecSetPublisher> logger) : ISpecSetPublisher
{
    public async Task<CommandResult> PublishAsync(
        PipelineContext pipeline, string project, SeriesScope scope,
        SpecSet set, IReadOnlyList<IgnoredInstruction> ignoredInstructions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);
        await refusals.ReportAsync(pipeline, ignoredInstructions, cancellationToken);
        var placement = placer.Place(set, scope);
        if (placement.Refusal is { } refused)
            return CommandResult.Fail($"Spec {set.Key} cannot be written: {refused}");
        PublishToContext(pipeline, placement);
        await CacheForViewerAsync(pipeline, placement.Set, cancellationToken);

        var write = await writer.WriteAsync(pipeline, placement, cancellationToken);
        if (!write.Written)
            return await NotWrittenAsync(project, placement, write, cancellationToken);
        pipeline.Set(ContextKeys.SpecRevisionSha, write.CommitSha!);
        await pointer.RecordAsync(project, placement.Carrier?.Name, placement.Set, write.Shas, cancellationToken);
        await prOpener.OpenAsync(pipeline, placement, scope, cancellationToken);
        return CommandResult.Ok(Describe(placement.Set));
    }

    // 2026-10-12-f5f7f: a multi-repository series is never worked from memory — a half nobody can
    // read back would be re-stamped and pushed by the record later. The revert shas are recorded so
    // the next run reads our own revert, not a reviewer's edit.
    private async Task<CommandResult> NotWrittenAsync(
        string project, SeriesPlacement placement, SpecSetWriteResult write, CancellationToken ct)
    {
        var key = placement.Set.Key;
        logger.LogWarning("Spec set {Key} could not be committed: {Error}", key, write.Error);
        await pointer.RecordShasAsync(project, key, write.RevertShas, ct);
        return write.IsRefused || write.IsRolledBack || placement.Members.Count > 1
            ? CommandResult.Fail($"Spec {key} cannot be written: {write.Error}")
            : CommandResult.Ok($"Spec derived but not committed ({write.Error}) — the run continues from it in-memory");
    }

    private static void PublishToContext(PipelineContext pipeline, SeriesPlacement placement)
    {
        pipeline.Set(ContextKeys.SpecSet, placement.Set);
        if (placement.Carrier is { } carrier)
            pipeline.Set(ContextKeys.SpecRepo, carrier.Name);
        if (placement.Set.Handback is { } handback && handback.Case != SpecHandbackCase.None)
            pipeline.Set(ContextKeys.SpecHandback, handback);
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
