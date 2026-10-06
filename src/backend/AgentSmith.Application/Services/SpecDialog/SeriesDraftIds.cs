using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-06-03c7c: gives approved drafts their series' ids before anything renders them. The
/// model's ids are placeholders; in the order the orderer put the drafts in, each becomes the
/// series' base plus a letter, every <c>requires:</c> edge follows it, and the draft's YAML text is
/// rewritten with the same map so the text and the fields say the same thing.
/// </summary>
public sealed class SeriesDraftIds(PhaseDraftIdRewriter rewriter)
{
    public IReadOnlyList<PhaseDraft> Assign(string series, IReadOnlyList<PhaseDraft> ordered)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(series);
        ArgumentNullException.ThrowIfNull(ordered);
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < ordered.Count; i++)
            ids.TryAdd(ordered[i].PhaseId, SeriesIdFactory.Member(series, i));
        return [.. ordered.Select(draft => draft with
        {
            PhaseId = rewriter.RewriteValue(draft.PhaseId, ids),
            Requires = [.. draft.Requires.Select(r => rewriter.RewriteValue(r, ids))],
            Yaml = rewriter.Rewrite(draft.Yaml, ids),
        })];
    }
}
