using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs.Reports;

/// <summary>
/// 2026-11-22-96c8a: what the report reads of a run add up to — each spec's follow-ups over all its passes (a
/// structure correction, every stage repair), one per goal ignoring case, and the run's refused instructions over
/// every spec, one per quote. Appended after each read; nothing ever removes an entry.
/// </summary>
public static class MasterReportFacts
{
    public static IReadOnlyList<MasterFollowUp>? FollowUps(PipelineContext pipeline, string specId) =>
        pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<MasterFollowUp>>>(ContextKeys.SpecFollowUps, out var all)
        && all is not null && all.TryGetValue(specId, out var held) ? held : null;

    public static void AddFollowUps(PipelineContext pipeline, string specId, IReadOnlyList<MasterFollowUp> read)
    {
        var all = pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<MasterFollowUp>>>(ContextKeys.SpecFollowUps, out var held)
            && held is not null ? new Dictionary<string, IReadOnlyList<MasterFollowUp>>(held, StringComparer.Ordinal)
            : new Dictionary<string, IReadOnlyList<MasterFollowUp>>(StringComparer.Ordinal);
        var merged = (all.TryGetValue(specId, out var earlier) ? earlier : []).ToList();
        foreach (var f in read)
            if (!merged.Any(m => string.Equals(m.Goal, f.Goal, StringComparison.OrdinalIgnoreCase))) merged.Add(f);
        all[specId] = merged;
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<MasterFollowUp>>>(ContextKeys.SpecFollowUps, all);
    }

    public static IReadOnlyList<IgnoredInstruction> Ignored(PipelineContext pipeline) =>
        pipeline.TryGet<IReadOnlyList<IgnoredInstruction>>(ContextKeys.RunIgnoredInstructions, out var held) && held is not null
            ? held : [];

    public static void AddIgnored(PipelineContext pipeline, IReadOnlyList<IgnoredInstruction> read)
    {
        var merged = Ignored(pipeline).ToList();
        foreach (var i in read)
            if (!merged.Any(m => string.Equals(m.Quote, i.Quote, StringComparison.Ordinal))) merged.Add(i);
        pipeline.Set<IReadOnlyList<IgnoredInstruction>>(ContextKeys.RunIgnoredInstructions, merged);
    }
}
