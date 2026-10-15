using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Contracts;

/// <summary>
/// 2026-10-15-4a4dd: a <see cref="ProjectScope"/> as a query filter. Every project returns the query itself and one
/// name emits the equality a ?project= filter emitted before — its parameter named as it was — so an installation
/// without enforcement and every global holder run exactly the SQL they ran; several names filter with a collection.
/// </summary>
public static class ProjectScopeQueries
{
    public static IQueryable<Run> Within(this IQueryable<Run> runs, ProjectScope scope) => scope.Only switch
    {
        null => runs,
        { Count: 1 } one => Equal(runs, one[0]),
        var projects => runs.Where(r => projects.Contains(r.Project)),
    };

    public static IQueryable<PrReviewRecord> Within(this IQueryable<PrReviewRecord> records, ProjectScope scope) => scope.Only switch
    {
        null => records,
        { Count: 1 } one => Equal(records, one[0]),
        var projects => records.Where(r => projects.Contains(r.Project)),
    };

    public static IQueryable<ApprovedSeries> Within(this IQueryable<ApprovedSeries> filings, ProjectScope scope) => scope.Only switch
    {
        null => filings,
        { Count: 1 } one => Equal(filings, one[0]),
        var projects => filings.Where(a => projects.Contains(a.Project)),
    };

    /// <summary>A conversation not yet grounded on a project carries nothing of one, so it stays its owner's.</summary>
    public static IQueryable<SpecDialogSession> Within(this IQueryable<SpecDialogSession> sessions, ProjectScope scope) =>
        scope.Only is { } projects ? sessions.Where(s => s.Project == "" || projects.Contains(s.Project)) : sessions;

    private static IQueryable<Run> Equal(IQueryable<Run> runs, string project) => runs.Where(r => r.Project == project);

    private static IQueryable<PrReviewRecord> Equal(IQueryable<PrReviewRecord> records, string project) =>
        records.Where(r => r.Project == project);

    private static IQueryable<ApprovedSeries> Equal(IQueryable<ApprovedSeries> filings, string project) =>
        filings.Where(a => a.Project == project);
}
