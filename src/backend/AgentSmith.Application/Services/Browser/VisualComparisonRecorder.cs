using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283di: keeps a run's comparisons for its result.md and writes at most
/// <see cref="MaxImagesPerRun"/> diff images under the run record's <c>compare/</c> directory in the
/// first repository — the run record is force-staged, so they reach the pull request beside the
/// numbers. An image that cannot be written is logged and the comparison is kept without it:
/// recording a report never fails the tool.
/// </summary>
public sealed class VisualComparisonRecorder(ISandboxBinaryFileWriter bytes, ILogger<VisualComparisonRecorder> logger)
{
    internal const int MaxImagesPerRun = 4;

    /// <summary>The comparisons the run made so far, oldest first.</summary>
    public static IReadOnlyList<VisualComparison> Recorded(PipelineContext pipeline) =>
        pipeline.TryGet<IReadOnlyList<VisualComparison>>(ContextKeys.VisualComparisons, out var kept) && kept is not null ? kept : [];

    public async Task<VisualComparison> RecordAsync(
        PipelineContext pipeline, ISandbox? repo, VisualComparison comparison, IReadOnlyList<(string Viewport, byte[] Jpeg)> diffs,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var kept = Recorded(pipeline);
        var room = MaxImagesPerRun - kept.Sum(c => c.Images.Count);
        var images = repo is null || room <= 0 || !pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId)
            ? [] : await WriteAsync(repo, RunRecordPaths.RelativeDir(runId!) + "/compare", kept.Count + 1, diffs.Take(room).ToList(), ct);
        var recorded = comparison with { Images = images };
        pipeline.Set<IReadOnlyList<VisualComparison>>(ContextKeys.VisualComparisons, [.. kept, recorded]);
        return recorded;
    }

    private async Task<IReadOnlyList<string>> WriteAsync(
        ISandbox repo, string dir, int number, IReadOnlyList<(string Viewport, byte[] Jpeg)> diffs, CancellationToken ct)
    {
        // 2026-10-08-e8b9j: each JPEG goes as bytes, decoded by the receiver — no python step.
        try
        {
            var paths = new List<string>();
            foreach (var (viewport, jpeg) in diffs)
            {
                var name = $"{number:00}-{viewport}.jpg";
                if (await bytes.WriteAsync(repo, dir, name, jpeg, ct) is { } failed)
                {
                    logger.LogWarning("Writing the comparison's diff images failed: {Error}", failed);
                    return [];
                }
                paths.Add($"{dir}/{name}");
            }
            return paths;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Writing the comparison's diff images failed");
        }
        return [];
    }
}
