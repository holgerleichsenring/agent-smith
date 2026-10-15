using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Persistence.Contracts;

/// <summary>
/// 2026-10-15-4a4dd: the projects one read may hold — every project, or the named ones in the configured spelling
/// the store holds. A list asks it before its Take, so a scoped caller gets a full page of their own projects.
/// <see cref="ProjectScopeQueries"/> turns it into the query's filter.
/// </summary>
public sealed class ProjectScope
{
    private ProjectScope(IReadOnlyList<string>? only) => Only = only;

    /// <summary>Every project — the query unchanged.</summary>
    public static ProjectScope Everything { get; } = new(null);

    /// <summary>The admitted names; null for every project.</summary>
    public IReadOnlyList<string>? Only { get; }

    public static ProjectScope Single(string name) => new([name]);

    public static ProjectScope Of(IEnumerable<string> names) => new([.. names]);

    public bool Admits(string name) => Only is null || Only.Contains(name, ConfigNames.Comparer);

    /// <summary>
    /// A request naming one project inside this scope: blank keeps the scope, every project narrows to the name as
    /// asked (today's filter), a scoped one to its own spelling of it — or to none, which reads as an unknown project.
    /// </summary>
    public ProjectScope Narrowed(string? requested)
    {
        if (string.IsNullOrEmpty(requested)) return this;
        if (Only is null) return Single(requested);
        var own = Only.FirstOrDefault(name => ConfigNames.AreSame(name, requested));
        return own is null ? Of([]) : Single(own);
    }
}
