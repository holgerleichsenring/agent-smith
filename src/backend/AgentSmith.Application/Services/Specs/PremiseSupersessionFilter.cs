using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-04-6768a: splits a premise check's false findings into those the series' own work superseded
/// and those that stay false. The superseded ones become one line, published as the spec's progress note
/// (state in progress), so a reader sees why a premise reported false did not stop the spec.
/// </summary>
public sealed class PremiseSupersessionFilter(
    PremiseBaseReplay replay, ISpecProgressRecorder progress, ILogger<PremiseSupersessionFilter> logger)
{
    public async Task<PremiseSorting> SortAsync(
        PipelineContext pipeline, SpecDraft draft, IReadOnlyList<PremiseFinding> findings, DerivationLook look,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(findings);
        if (findings.Count == 0 || !pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) || set is null)
            return new PremiseSorting(findings, null);
        var stillFalse = new List<PremiseFinding>();
        var superseded = new List<PremiseSupersession>();
        foreach (var finding in findings)
        {
            if (await replay.SupersededAsync(pipeline, set, look, finding, cancellationToken) is { } gone)
                superseded.Add(gone);
            else stillFalse.Add(finding);
        }
        if (superseded.Count == 0) return new PremiseSorting(stillFalse, null);
        var line = Line(draft.SpecId, superseded, PrecedingSpecs.Of(pipeline, draft.SpecId));
        await progress.RecordAsync(pipeline, draft.SpecId, SpecRunState.InProgress, note: line,
            cancellationToken: cancellationToken);
        logger.LogInformation("{Line}", line);
        return new PremiseSorting(stillFalse, line);
    }

    // The step result's and progress note's line for every superseded premise of one spec.
    private static string Line(
        string specId, IReadOnlyList<PremiseSupersession> superseded, IReadOnlyList<SpecProgress> preceding)
    {
        var after = preceding.Count == 0 ? "no preceding spec" : string.Join(", ", preceding.Select(p => p.SpecId));
        return string.Join(" | ", superseded.Select(s =>
            $"Premise superseded in {specId}: \"{s.Finding.Premise.Trim()}\" — absent/present flipped between "
            + $"{Short(s.Base)} and HEAD in {string.Join(", ", s.Paths)}, by "
            + $"{string.Join(",", s.Commits.Select(Short))} after {after} ran"));
    }

    private static string Short(string revision)
    {
        var sha = revision.TrimEnd('^');
        return (sha.Length > 8 ? sha[..8] : sha) + revision[sha.Length..];
    }
}
