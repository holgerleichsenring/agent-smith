using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-17-7c9ba: puts a placed set where the run reads it — the context slots (set, carrying
/// repository, hand-back) and the run view's spec panel. Shared by the publish of a new revision and
/// the keep of an unchanged one, which differ only in whether the branch is written.
/// </summary>
public sealed class SpecSetContextPublication(
    Contracts.Persistence.IRunArtifactStore artifacts,
    ILogger<SpecSetContextPublication> logger)
{
    public async Task PublishAsync(PipelineContext pipeline, SeriesPlacement placement, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(placement);
        pipeline.Set(ContextKeys.SpecSet, placement.Set);
        if (placement.Carrier is { } carrier)
            pipeline.Set(ContextKeys.SpecRepo, carrier.Name);
        if (placement.Set.Handback is { } handback && handback.Case != SpecHandbackCase.None)
            pipeline.Set(ContextKeys.SpecHandback, handback);
        await CacheForViewerAsync(pipeline, placement.Set, ct);
    }

    // The run detail reads this slot exactly as it reads plan.md. Best-effort: a
    // cold cache costs the viewer a panel, never the run.
    private async Task CacheForViewerAsync(PipelineContext pipeline, SpecSet set, CancellationToken ct)
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
}
