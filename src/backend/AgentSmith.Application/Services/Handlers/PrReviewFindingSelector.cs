using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Applies the pr-review render budget: filters runtime-emitted markers out,
/// groups line-anchored findings by file + line range, sorts groups most
/// severe first, and keeps the top <see cref="MaxInlineComments"/> as inline
/// candidates. Everything else (excess groups, unanchored findings, anchors
/// outside the PR diff's new-side lines) folds into the summary — a comment
/// on a line the diff doesn't touch would be rejected by the platform API.
/// </summary>
public sealed class PrReviewFindingSelector
{
    public const int MaxInlineComments = 25;

    public PrReviewFindingSelection Select(
        IReadOnlyList<SkillObservation> observations, PrDiffAnalysis diff)
    {
        var commentableLines = BuildCommentableLines(diff);
        var findings = observations
            .Where(o => !ExecutionLimitCategories.IsExecutionLimit(o.Category))
            .Select(o => (Observation: o, Anchor: ResolveAnchor(o, commentableLines)))
            .ToList();

        var groups = GroupByAnchor(findings);
        var summaryOnly = groups.Skip(MaxInlineComments).SelectMany(g => g.Observations)
            .Concat(findings.Where(f => f.Anchor is null).Select(f => f.Observation))
            .OrderBy(o => o.Severity)
            .ToList();

        return new PrReviewFindingSelection(
            groups.Take(MaxInlineComments).ToList(), summaryOnly, findings.Count);
    }

    private static List<PrReviewFindingGroup> GroupByAnchor(
        IReadOnlyList<(SkillObservation Observation, (string File, int Start, int End)? Anchor)> findings)
        => findings
            .Where(f => f.Anchor is not null)
            .GroupBy(f => f.Anchor!.Value)
            .Select(g => new PrReviewFindingGroup(
                g.Key.File, g.Key.Start, g.Key.End,
                g.Select(f => f.Observation).OrderBy(o => o.Severity).ToList()))
            .OrderBy(g => g.TopSeverity).ThenBy(g => g.File, StringComparer.Ordinal).ThenBy(g => g.StartLine)
            .ToList();

    private static (string File, int Start, int End)? ResolveAnchor(
        SkillObservation observation, IReadOnlyDictionary<string, HashSet<int>> commentableLines)
    {
        if (string.IsNullOrWhiteSpace(observation.File)) return null;
        var (start, end) = observation.LineRange is { } range
            ? (range.Start, range.End)
            : (observation.StartLine, observation.EndLine ?? observation.StartLine);
        if (start < 1 || end < start) return null;

        if (!commentableLines.TryGetValue(observation.File, out var lines)) return null;
        for (var line = start; line <= end; line++)
            if (!lines.Contains(line)) return null;
        return (observation.File, start, end);
    }

    private static Dictionary<string, HashSet<int>> BuildCommentableLines(PrDiffAnalysis diff)
        => diff.Files.ToDictionary(
            f => f.Path,
            f => f.Hunks.SelectMany(h => h.Lines)
                .Where(l => l.NewLineNumber is not null)
                .Select(l => l.NewLineNumber!.Value)
                .ToHashSet(),
            StringComparer.Ordinal);
}
