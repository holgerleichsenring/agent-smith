using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: reads the spec set back off the checked-out ticket branch — the FIRST source in the
/// precedence, ahead of deriving one.
/// <para>
/// 2026-09-22-6ad7: it says WHICH nothing it found. No manifest is a branch nobody has written the
/// set to yet; a manifest that does not parse, a listed spec that does not read back and a
/// carrying repository this run never checked out are all "something is there and this run cannot
/// read it" — the caller hands those back rather than replacing them from a copy.
/// </para>
/// <para>
/// 2026-10-06-03c7d/e: the set is the series whose manifest under <c>series/</c> names the run's
/// ticket key; a spec is executed when it lies in done/.
/// </para>
/// <para>
/// 2026-10-12-f5f7b: every run repository may hold its own self-contained sub-series of the ticket.
/// Each is read as one repository was read before (<see cref="SubSeriesReader"/>) and the reads are
/// joined into one set (<see cref="SeriesJoin"/>); the set carries the last commit per joined
/// repository and names the lagging members.
/// </para>
/// <para>
/// 2026-11-17-a0b6a: the join and the composition are <see cref="SeriesComposer"/>'s, shared with the
/// host-side branch read (<see cref="TicketBranchSeries"/>).
/// </para>
/// </summary>
public sealed class SeriesReader(SubSeriesReader subSeries, SeriesComposer composer) : ISpecSetReader
{
    public async Task<SpecSetOnBranch> ReadAsync(
        PipelineContext pipeline, RepoConnection carryingRepo, IReadOnlyList<RepoConnection> repositories,
        TicketKey ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(carryingRepo);
        var reads = new List<SubSeriesRead>();
        foreach (var repo in RunRepos(carryingRepo, repositories))
            reads.Add(await subSeries.ReadAsync(pipeline, repo, ticket, cancellationToken));
        return composer.Compose(ticket, carryingRepo.Name ?? string.Empty, reads);
    }

    /// <summary>The paths whose last commit is a series' revision sha.</summary>
    public static IReadOnlyList<string> RevisionPaths(string seriesBase) =>
        [SeriesPaths.Manifest(seriesBase), SeriesPaths.SpecFilesPathspec(seriesBase)];

    // The carrying repository is read even when the scope does not list it — the one-repository
    // reader read nothing else; a repository listed twice is read once. 2026-10-12-f5f7d: a series
    // nothing records a carrier for names it with no name, and nothing is read for it.
    private static IReadOnlyList<RepoConnection> RunRepos(
        RepoConnection carryingRepo, IReadOnlyList<RepoConnection> repositories) =>
        [.. new[] { carryingRepo }.Concat(repositories ?? [])
            .Where(r => !string.IsNullOrEmpty(r.Name))
            .DistinctBy(r => r.Name ?? string.Empty, StringComparer.Ordinal)];
}
