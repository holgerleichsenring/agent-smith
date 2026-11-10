using System.ComponentModel;
using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Memory;
using AgentSmith.Application.Services.Repositories;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Memory;
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
/// <para>2026-11-10-fcd7b: the project's own store is read first, as '@project', when the run's project has one.
/// 2026-11-10-4778 review: a project entry's body is QUOTED and followed by where it came from, because no review
/// stood between the person or run that said it and this prompt.</para>
/// </summary>
public sealed class MemoryRecallToolHost(
    IRunRepositories repositories, string repoRoot, MemoryStandingReader standings, PipelineContext pipeline,
    ILogger? logger = null, MemorySources? sources = null)
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

    [Description("Recalls stored experiential memories matching the query, from the project's own store and every " +
                 "repository of the run. Use before re-deriving a known fact. Supports plain text, [[slug]] citations, and " +
                 "a type:feedback|project|reference facet filter. Returns full memory bodies, each with its track " +
                 "record; a memory the runs contradicted is marked DISPUTED and must not be acted on.")]
    public async Task<string> Recall(
        [Description("Search text — e.g. 'sandbox timeout', '[[no-wrapper-shims]]', 'type:feedback pricing'.")]
        string query,
        CancellationToken ct = default)
    {
        IReadOnlyList<MemorySource> stores = sources is null
            ? [.. repositories.Names().Where(n => repositories.FilesOf(n) is not null).Select(n =>
                new MemorySource(n, n, new FileMemoryStore(repositories.FilesOf(n)!, repoRoot, logger)))]
            : await sources.OfAsync(pipeline, repositories, repoRoot, logger, ct);
        var matches = new List<(string Repository, MemoryEntry Entry, MemoryStanding Standing, ProjectMemoryRecord? Kept)>();
        foreach (var source in stores)
        {
            var found = await source.Store.SearchAsync(query, ct);
            if (found.Count == 0) continue;
            var record = source.IsProject
                ? await standings.OfKeyAsync(source.LedgerKey, source.Label, ct)
                : await standings.OfAsync(pipeline, source.Label, ct);
            var kept = source.Store is ProjectMemoryStore project
                ? (await project.RecordsAsync(ct)).ToDictionary(r => r.Slug, StringComparer.Ordinal)
                : null;
            foreach (var entry in found)
                matches.Add((source.Label, entry, record.GetValueOrDefault(entry.Name) ?? MemoryStanding.None(entry.Name),
                    kept?.GetValueOrDefault(entry.Name)));
        }
        if (matches.Count == 0)
            return $"No memories matched '{query}'. The index in your context lists what exists.";
        return Format(matches, labelled: stores.Count > 1);
    }

    private static string Format(
        IReadOnlyList<(string Repository, MemoryEntry Entry, MemoryStanding Standing, ProjectMemoryRecord? Kept)> matches,
        bool labelled)
    {
        var sb = new StringBuilder();
        foreach (var (repository, entry, standing, kept) in matches)
        {
            sb.AppendLine($"## {entry.Name} ({MemoryEntryTypes.ToSlug(entry.Type)}, {standing.Label} · {standing.Record})"
                          + (labelled ? $" — {Source(repository)}" : string.Empty));
            if (standing.Label == MemoryStandingLabels.Disputed) sb.AppendLine(DisputedMarker);
            sb.AppendLine(entry.Description);
            sb.AppendLine();
            if (kept is null) sb.AppendLine(entry.Body);
            else sb.AppendLine(Quote(entry.Body)).AppendLine(Provenance(kept));
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static string Quote(string body) =>
        string.Join('\n', body.ReplaceLineEndings("\n").Split('\n').Select(line => $"> {line}"));

    /// <summary>Where a project memory came from, as one line under its quoted body.</summary>
    public static string Provenance(ProjectMemoryRecord kept)
    {
        ArgumentNullException.ThrowIfNull(kept);
        var by = string.IsNullOrWhiteSpace(kept.Author) ? string.Empty : $", by {kept.Author}";
        return kept.SourceKind == ProjectMemorySourceKinds.Run
            ? $"(kept on the project from run {kept.SourceId}{(kept.SourceRef is null ? string.Empty : $", spec {kept.SourceRef}")}{by})"
            : $"(kept on the project from conversation {kept.SourceId}{(kept.SourceRef is null ? string.Empty : $", turn {kept.SourceRef}")}{by})";
    }

    private static string Source(string label) =>
        label == ProjectMemorySource.Label ? "the project" : $"repository {label}";
}
