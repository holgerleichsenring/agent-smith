using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Pipeline;

/// <summary>
/// 2026-11-27-3532d: a spec retired while its block runs hands the rest of the block to its replacement — the steps
/// verify and record the replacement (they read CurrentSpec) — so the steps still ahead carry the replacement's id:
/// their labels, the events they publish and the passes read per spec name the spec they work. Read off the set, so a
/// resumed run's restored steps are stamped the same way.
/// </summary>
public static class SpecBlockRestamp
{
    /// <summary>Stamps <paramref name="from"/> and every step after it whose spec was retired with the spec that
    /// stands in its place now.</summary>
    public static void Apply(LinkedListNode<PipelineCommand> from, PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(context);
        if (!context.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) || set is null) return;
        var successor = set.Specs.SelectMany(s => s.Draft.Supersedes.Select(old => (Old: old, New: s.SpecId)))
            .GroupBy(p => p.Old, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last().New, StringComparer.Ordinal);
        if (successor.Count == 0) return;
        for (var node = from; node is not null; node = node.Next)
            if (node.Value.SpecId is { } id && successor.ContainsKey(id))
                node.Value = node.Value with { SpecId = Standing(id, successor) };
    }

    // The end of the chain: a replacement retired in its turn hands on to its own replacement.
    private static string Standing(string id, IReadOnlyDictionary<string, string> successor)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (successor.TryGetValue(id, out var next) && seen.Add(id)) id = next;
        return id;
    }
}
