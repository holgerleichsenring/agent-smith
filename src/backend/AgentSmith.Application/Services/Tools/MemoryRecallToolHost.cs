using System.ComponentModel;
using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Memory;
using AgentSmith.Application.Services.Repositories;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0380: the problem-time half of the memory model — recall(query) returns
/// the BODIES of matching memories. A pure READ, so it joins every master
/// surface including the read-only Review/scan surface. Grep-first facet +
/// text match; no vector index.
/// <para>
/// 2026-10-12-f5f7c: every repository of the run keeps its own store, so recall reads them all
/// and names the repository each entry came from when there is more than one.
/// </para>
/// </summary>
public sealed class MemoryRecallToolHost(IRunRepositories repositories, string repoRoot, ILogger? logger = null)
    : IToolHost
{
    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(Recall, name: "recall")];
    }

    [Description("Recalls stored experiential memories matching the query, from every repository of the " +
                 "run. Use before re-deriving a known fact. Supports plain text, [[slug]] citations, and " +
                 "a type:feedback|project|reference facet filter. Returns full memory bodies.")]
    public async Task<string> Recall(
        [Description("Search text — e.g. 'sandbox timeout', '[[no-wrapper-shims]]', 'type:feedback pricing'.")]
        string query,
        CancellationToken ct = default)
    {
        var names = repositories.Names();
        var matches = new List<(string Repository, MemoryEntry Entry)>();
        foreach (var name in names)
            if (repositories.FilesOf(name) is { } files)
                foreach (var entry in await new MemoryStore(files, repoRoot, logger).SearchAsync(query, ct))
                    matches.Add((name, entry));
        if (matches.Count == 0)
            return $"No memories matched '{query}'. The index in your context lists what exists.";
        return Format(matches, labelled: names.Count > 1);
    }

    private static string Format(IReadOnlyList<(string Repository, MemoryEntry Entry)> matches, bool labelled)
    {
        var sb = new StringBuilder();
        foreach (var (repository, entry) in matches)
        {
            sb.AppendLine($"## {entry.Name} ({MemoryEntryTypes.ToSlug(entry.Type)}"
                          + (entry.Status is null ? ")" : $", {entry.Status})")
                          + (labelled ? $" — repository {repository}" : string.Empty));
            sb.AppendLine(entry.Description);
            sb.AppendLine();
            sb.AppendLine(entry.Body);
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }
}
