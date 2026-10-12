using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

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
/// </summary>
public sealed class SeriesReader(
    SubSeriesReader subSeries,
    SeriesJoin join,
    SeriesManifest manifest,
    ILogger<SeriesReader> logger) : ISpecSetReader
{
    public async Task<SpecSetOnBranch> ReadAsync(
        PipelineContext pipeline, RepoConnection carryingRepo, IReadOnlyList<RepoConnection> repositories,
        TicketKey ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(carryingRepo);
        var carrying = carryingRepo.Name ?? string.Empty;
        var reads = new List<SubSeriesRead>();
        foreach (var repo in RunRepos(carryingRepo, repositories))
            reads.Add(await subSeries.ReadAsync(pipeline, repo, ticket, cancellationToken));
        var joined = join.Join(reads, carrying);
        if (joined.Otherwise is { } answer)
        {
            if (answer.State == SpecSetBranchState.Unreadable)
                logger.LogWarning("The series of ticket {Ticket} is unreadable: {Why}", ticket.Value, answer.Why);
            return answer;
        }
        return SpecSetOnBranch.Answered(ResultOf(ticket, carrying, joined));
    }

    /// <summary>The paths whose last commit is a series' revision sha.</summary>
    public static IReadOnlyList<string> RevisionPaths(string seriesBase) =>
        [SeriesPaths.Manifest(seriesBase), SeriesPaths.SpecFilesPathspec(seriesBase)];

    private SpecSetReadResult ResultOf(TicketKey ticket, string carrying, SeriesJoined joined)
    {
        var newest = joined.Newest!;
        var set = SetOf(ticket, newest.Base, newest.Document!, joined.Phases, joined.Executed);
        var sha = joined.Shas.TryGetValue(carrying, out var own) ? own : joined.Shas[newest.Repo];
        logger.LogInformation(
            "Series {Base} of {Ticket} read from {Repos}: {Phases} spec(s), revision {Revision}, lagging {Lagging}",
            newest.Base, set.Key, string.Join(", ", joined.Shas.Keys), set.Phases.Count, set.Current.Number,
            joined.Lagging.Count == 0 ? "none" : string.Join(", ", joined.Lagging));
        return new SpecSetReadResult(set, sha) { RevisionShas = joined.Shas, Lagging = joined.Lagging };
    }

    // The carrying repository is read even when the scope does not list it — the one-repository
    // reader read nothing else; a repository listed twice is read once.
    private static IReadOnlyList<RepoConnection> RunRepos(
        RepoConnection carryingRepo, IReadOnlyList<RepoConnection> repositories) =>
        [.. new[] { carryingRepo }.Concat(repositories ?? [])
            .DistinctBy(r => r.Name ?? string.Empty, StringComparer.Ordinal)];

    private SpecSet SetOf(
        TicketKey ticket, string seriesBase, SeriesManifestDocument doc, IReadOnlyList<SpecPhase> phases,
        IReadOnlyList<string> executed) =>
        new(ticket.Value, phases, manifest.AccountingOf(doc), manifest.RevisionsOf(doc),
            SpecSource.BranchArtifact, manifest.HandbackOf(doc), doc.TicketPinnedWhole,
            executed, manifest.FingerprintOf(doc), manifest.ApprovalOf(doc), seriesBase)
        {
            Goal = string.IsNullOrWhiteSpace(doc.Goal) ? null : doc.Goal,
        };
}
