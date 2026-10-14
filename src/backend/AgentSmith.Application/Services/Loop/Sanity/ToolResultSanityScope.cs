namespace AgentSmith.Application.Services.Loop.Sanity;

/// <summary>
/// What the sanity rules judge one tool result against: the paths the current spec cites, the
/// repositories it names, and the run's sandbox-key to repository-name map. Read from the
/// pipeline on every call, so a spec selected mid-run is the one judged.
/// </summary>
public sealed record ToolResultSanityScope(
    IReadOnlyList<ScopedPath> CitedPaths,
    IReadOnlyList<string> NamedRepositories,
    IReadOnlyDictionary<string, string> KeyToRepository)
{
    private const string WorkRoot = "/work";

    /// <summary>The one repository of a single-repository run; null on a multi-repository one.</summary>
    public string? SingleRepository =>
        KeyToRepository.Values.Distinct(StringComparer.Ordinal).ToList() is [var only] ? only : null;

    /// <summary>
    /// A tool's path as the rules compare it: a leading ./ or /work/ dropped, and a leading
    /// repository name or sandbox key — the longest one the path starts with — stripped and kept
    /// as the path's repository.
    /// </summary>
    public ScopedPath Normalise(string? path)
    {
        var bare = Unrooted((path ?? string.Empty).Trim());
        var key = KeyToRepository.Keys.Concat(KeyToRepository.Values)
            .Where(k => k.Length > 0 && (bare == k || bare.StartsWith(k + "/", StringComparison.Ordinal)))
            .OrderByDescending(k => k.Length).FirstOrDefault();
        if (key is null) return new ScopedPath(null, Tidy(bare));
        var repository = KeyToRepository.TryGetValue(key, out var name) ? name : key;
        return new ScopedPath(repository, Tidy(Unrooted(bare[key.Length..].TrimStart('/'))));
    }

    private static string Unrooted(string path)
    {
        while (true)
        {
            if (path.StartsWith("./", StringComparison.Ordinal)) path = path[2..];
            else if (path == WorkRoot) path = ".";
            else if (path.StartsWith(WorkRoot + "/", StringComparison.Ordinal)) path = path[(WorkRoot.Length + 1)..];
            else return path;
        }
    }

    private static string Tidy(string path)
    {
        var trimmed = path.TrimEnd('/');
        return trimmed.Length == 0 ? "." : trimmed;
    }
}

/// <summary>A normalised path and the repository its prefix named, if it named one.</summary>
public sealed record ScopedPath(string? Repository, string Path)
{
    /// <summary>
    /// True when this path is <paramref name="cited"/> or lies inside it, and the repositories
    /// agree or either side has none. An ancestor of a cited path is not inside it.
    /// </summary>
    public bool LiesIn(ScopedPath cited) =>
        (Repository is null || cited.Repository is null
            || string.Equals(Repository, cited.Repository, StringComparison.OrdinalIgnoreCase))
        && cited.Path != "."
        && (Path == cited.Path || Path.StartsWith(cited.Path + "/", StringComparison.Ordinal));
}
