using System.ComponentModel;
using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Memory;
using AgentSmith.Application.Services.Repositories;
using AgentSmith.Contracts.Commands;
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
/// <para>
/// 2026-10-25-6c1f: each match is headed with the standing and record the index shows, read per repository; a
/// disputed match leads with <see cref="DisputedMarker"/> and keeps its body, so the master sees why it misled.
/// </para>
/// </summary>
public sealed class MemoryRecallToolHost(
    IRunRepositories repositories, string repoRoot, MemoryStandingReader standings, PipelineContext pipeline,
    ILogger? logger = null)
    : IToolHost
{
    public const string DisputedMarker =
        "DISPUTED: the runs contradicted this memory; do not act on it. Its text is kept only to show why it misled.";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(Recall, name: "recall")];
    }

    [Description("Recalls stored experiential memories matching the query, from every repository of the " +
                 "run. Use before re-deriving a known fact. Supports plain text, [[slug]] citations, and " +
                 "a type:feedback|project|reference facet filter. Returns full memory bodies, each with its track " +
                 "record; a memory the runs contradicted is marked DISPUTED and must not be acted on.")]
    public async Task<string> Recall(
        [Description("Search text — e.g. 'sandbox timeout', '[[no-wrapper-shims]]', 'type:feedback pricing'.")]
        string query,
        CancellationToken ct = default)
    {
        var names = repositories.Names();
        var matches = new List<(string Repository, MemoryEntry Entry, MemoryStanding Standing)>();
        foreach (var name in names)
        {
            if (repositories.FilesOf(name) is not { } files) continue;
            var found = await new MemoryStore(files, repoRoot, logger).SearchAsync(query, ct);
            if (found.Count == 0) continue;
            var record = await standings.OfAsync(pipeline, name, ct);
            foreach (var entry in found)
                matches.Add((name, entry, record.GetValueOrDefault(entry.Name) ?? MemoryStanding.None(entry.Name)));
        }
        if (matches.Count == 0)
            return $"No memories matched '{query}'. The index in your context lists what exists.";
        return Format(matches, labelled: names.Count > 1);
    }

    private static string Format(
        IReadOnlyList<(string Repository, MemoryEntry Entry, MemoryStanding Standing)> matches, bool labelled)
    {
        var sb = new StringBuilder();
        foreach (var (repository, entry, standing) in matches)
        {
            sb.AppendLine($"## {entry.Name} ({MemoryEntryTypes.ToSlug(entry.Type)}, {standing.Label} · {standing.Record})"
                          + (labelled ? $" — repository {repository}" : string.Empty));
            if (standing.Label == MemoryStandingLabels.Disputed) sb.AppendLine(DisputedMarker);
            sb.AppendLine(entry.Description);
            sb.AppendLine();
            sb.AppendLine(entry.Body);
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }
}
