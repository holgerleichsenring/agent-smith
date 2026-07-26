using System.ComponentModel;
using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Memory;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0380: the problem-time half of the memory model — recall(query) returns
/// the BODIES of matching memories. A pure READ, so it joins every master
/// surface including the read-only Review/scan surface. Grep-first facet +
/// text match; no vector index.
/// </summary>
public sealed class MemoryRecallToolHost(MemoryStore store) : IToolHost
{
    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(Recall, name: "recall")];
    }

    [Description("Recalls stored experiential memories matching the query. Use before " +
                 "re-deriving a known fact. Supports plain text, [[slug]] citations, and " +
                 "a type:feedback|project|reference facet filter. Returns full memory bodies.")]
    public async Task<string> Recall(
        [Description("Search text — e.g. 'sandbox timeout', '[[no-wrapper-shims]]', 'type:feedback pricing'.")]
        string query,
        CancellationToken ct = default)
    {
        var matches = await store.SearchAsync(query, ct);
        if (matches.Count == 0)
            return $"No memories matched '{query}'. The index in your context lists what exists.";
        return Format(matches);
    }

    private static string Format(IReadOnlyList<MemoryEntry> matches)
    {
        var sb = new StringBuilder();
        foreach (var entry in matches)
        {
            sb.AppendLine($"## {entry.Name} ({MemoryEntryTypes.ToSlug(entry.Type)}"
                          + (entry.Status is null ? ")" : $", {entry.Status})"));
            sb.AppendLine(entry.Description);
            sb.AppendLine();
            sb.AppendLine(entry.Body);
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }
}
