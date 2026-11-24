using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Contracts;

/// <summary>
/// 2026-10-15-4a4dd: a <see cref="ProjectScope"/> as a query filter. Every project returns the query itself and one
/// name emits the equality a ?project= filter emitted before — its parameter named as it was — so an installation
/// without enforcement and every global holder run exactly the SQL they ran; several names filter with a collection.
/// <para>2026-11-23-47b6: one rule for every project-owned row — a run, a pull-request record and a filed series
/// leave out the same hidden projects (2026-11-17-f3a8a); 2026-11-23-8df6a: and a deploy record.</para>
/// </summary>
public static class ProjectScopeQueries
{
    public static IQueryable<T> Within<T>(this IQueryable<T> rows, ProjectScope scope) where T : class, IProjectOwned =>
        scope.Only switch
        {
            null when scope.Hidden.Count > 0 => Hiding(rows, scope.Hidden),
            null => rows,
            { Count: 1 } one => Equal(rows, one[0]),
            var projects => rows.Where(r => projects.Contains(r.Project)),
        };

    /// <summary>A conversation not yet grounded on a project carries nothing of one, so it stays its owner's.</summary>
    public static IQueryable<SpecDialogSession> Within(this IQueryable<SpecDialogSession> sessions, ProjectScope scope) =>
        scope.Only is { } projects ? sessions.Where(s => s.Project == "" || projects.Contains(s.Project)) : sessions;

    private static IQueryable<T> Hiding<T>(IQueryable<T> rows, IReadOnlyList<string> hidden) where T : class, IProjectOwned =>
        rows.Where(r => !hidden.Contains(r.Project));

    private static IQueryable<T> Equal<T>(IQueryable<T> rows, string project) where T : class, IProjectOwned =>
        rows.Where(r => r.Project == project);
}
