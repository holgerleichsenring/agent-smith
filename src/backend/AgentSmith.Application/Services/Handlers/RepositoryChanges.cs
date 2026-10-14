namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// What each repository's branch changed, read once per verification, and which of its
/// declared stages that left anything to run.
/// <para>
/// A repository is every sandbox serving it; its changed paths are the union of theirs. A
/// repository whose list could not be read in full is UNDETERMINED, and an undetermined
/// repository touches every glob — unknown is not unchanged.
/// </para>
/// </summary>
public sealed class RepositoryChanges
{
    private readonly IReadOnlyDictionary<string, string> _repositoryOf;
    private readonly IReadOnlyDictionary<string, IReadOnlySet<string>> _paths;
    private readonly IReadOnlySet<string> _undetermined;
    private readonly HashSet<string> _eligible = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _untouchedGlobs = new(StringComparer.Ordinal);

    public RepositoryChanges(
        IReadOnlyDictionary<string, string> repositoryOf,
        IReadOnlyDictionary<string, IReadOnlySet<string>> paths,
        IReadOnlySet<string> undetermined)
    {
        _repositoryOf = repositoryOf;
        _paths = paths;
        _undetermined = undetermined;
    }

    /// <summary>The repository a sandbox serves; a sandbox placed in none is its own.</summary>
    public string RepositoryOf(string key) => _repositoryOf.GetValueOrDefault(key, key);

    public bool IsUndetermined(string key) => _undetermined.Contains(RepositoryOf(key));

    public IReadOnlySet<string> Paths(string key) =>
        _paths.TryGetValue(RepositoryOf(key), out var paths) ? paths : new HashSet<string>();

    /// <summary>True when the sandbox's repository changed a path one of the globs matches,
    /// or when what it changed could not be read.</summary>
    public bool Touches(string key, IReadOnlyList<string> globs) =>
        IsUndetermined(key)
        || Paths(key).Any(path => globs.Any(glob => ChangedPathGlob.IsMatch(glob, path)));

    /// <summary>The sandbox has at least one stage to run, so its repository answered.</summary>
    public void Ran(string key) => _eligible.Add(RepositoryOf(key));

    /// <summary>A stage of the sandbox was skipped because none of these globs matched.</summary>
    public void Untouched(string key, IReadOnlyList<string> globs)
    {
        var repository = RepositoryOf(key);
        if (!_untouchedGlobs.TryGetValue(repository, out var known))
            _untouchedGlobs[repository] = known = [];
        known.AddRange(globs.Where(glob => !known.Contains(glob, StringComparer.Ordinal)));
    }

    /// <summary>Every repository that had nothing to run and skipped at least one stage by its
    /// when_changed globs: no stage applies there, and the notes say so.</summary>
    public void Conclude(VerifyResolutionNotes notes)
    {
        ArgumentNullException.ThrowIfNull(notes);
        foreach (var (repository, globs) in _untouchedGlobs.OrderBy(e => e.Key, StringComparer.Ordinal))
            if (!_eligible.Contains(repository))
                notes.NothingApplies(repository, globs);
    }
}
