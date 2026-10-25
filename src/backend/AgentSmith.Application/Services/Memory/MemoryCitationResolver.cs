using AgentSmith.Application.Services.Repositories;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Memory;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-10-25-9324b: turns a master's memory entries — <c>slug</c> or <c>repository/slug</c> — into citations,
/// each checked against that repository's store. A bare slug names the one repository holding it; held by
/// none, or by several, the entry is named back as a problem, never guessed.
/// </summary>
public sealed class MemoryCitationResolver(MemoryRepositoryKey keys)
{
    public async Task<MemoryCitationRead> ResolveAsync(
        PipelineContext pipeline, IRunRepositories repositories, string repoRoot, IReadOnlyList<string> entries,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        var held = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        var citations = new List<MemoryCitation>();
        var problems = new List<string>();
        foreach (var entry in entries)
        {
            var (named, slug) = Split(entry);
            var candidates = repositories.Names()
                .Where(n => named is null || string.Equals(n, named, StringComparison.OrdinalIgnoreCase)).ToList();
            var holders = new List<string>();
            foreach (var name in candidates)
                if ((await SlugsAsync(repositories, repoRoot, name, held, ct)).Contains(slug)) holders.Add(name);
            if (holders.Count == 1) citations.Add(new MemoryCitation(holders[0], keys.Of(pipeline, holders[0]), slug));
            else problems.Add(Problem(entry, named, candidates.Count, holders, repositories.Names()));
        }
        return new MemoryCitationRead([.. citations.DistinctBy(c => (c.Repository, c.Slug))], problems);
    }

    private static (string? Repository, string Slug) Split(string entry)
    {
        var text = entry.Trim().TrimStart('[').TrimEnd(']');
        var at = text.LastIndexOf('/');
        return at > 0 ? (text[..at].Trim(), MemorySlug.ToKebab(text[(at + 1)..])) : (null, MemorySlug.ToKebab(text));
    }

    private static async Task<IReadOnlySet<string>> SlugsAsync(
        IRunRepositories repositories, string repoRoot, string name, Dictionary<string, IReadOnlySet<string>> held,
        CancellationToken ct)
    {
        if (held.TryGetValue(name, out var slugs)) return slugs;
        var entries = repositories.FilesOf(name) is { } files ? await new MemoryStore(files, repoRoot).ListAsync(ct) : [];
        return held[name] = entries.Select(e => e.Name).ToHashSet(StringComparer.Ordinal);
    }

    private static string Problem(
        string entry, string? named, int candidates, IReadOnlyList<string> holders, IReadOnlyList<string> names) =>
        named is not null && candidates == 0
            ? $"'{entry}' names no repository of this run [{string.Join(", ", names)}]"
            : holders.Count == 0
                ? $"'{entry}' is no stored memory — the index lists what exists"
                : $"'{entry}' is stored in [{string.Join(", ", holders)}] — name it as 'repository/slug'";
}
