using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
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
/// 2026-10-06-03c7d: the set is the series whose manifest under <c>series/</c> names the run's
/// ticket key; its specs are read from <c>specs/planned/</c> by id. The revision sha is the last
/// commit on the manifest and on every spec file of the series.
/// </para>
/// <para>
/// 2026-10-06-03c7e: specs are read across planned/, done/ and active/; a spec is executed when
/// it lies in done/ — the manifest keeps no executed list.
/// </para>
/// </summary>
public sealed class SeriesReader(
    ISandboxFileReaderFactory readerFactory,
    SandboxGitOperations gitOps,
    SeriesManifestFinder finder,
    SeriesSpecFileReader specs,
    SeriesManifest manifest,
    SandboxTargets sandboxTargets,
    ILogger<SeriesReader> logger) : ISpecSetReader
{
    public async Task<SpecSetOnBranch> ReadAsync(
        PipelineContext pipeline, RepoConnection carryingRepo, TicketKey ticket,
        CancellationToken cancellationToken)
    {
        var matches = sandboxTargets.SandboxesForRepo(pipeline, carryingRepo);
        if (matches.Count == 0)
        {
            // NOT "nothing at the path": the path was never looked at.
            logger.LogWarning(
                "No sandbox for {Repo} — the repository carrying the spec set is not checked out",
                carryingRepo.Name);
            return SpecSetOnBranch.Unreadable(
                $"the repository '{carryingRepo.Name}' carrying the set is not checked out in this run");
        }

        var sandbox = matches[0].Value;
        var files = readerFactory.Create(sandbox);
        var lookup = await finder.FindAsync(files, ticket, cancellationToken);
        if (lookup.Otherwise is { } answer) return answer;
        var phases = await ReadSpecsAsync(files, lookup.Document!, cancellationToken);
        if (phases.Why is { } why)
        {
            logger.LogWarning("Series {Base} did not read back: {Why}", lookup.Base, why);
            return SpecSetOnBranch.Unreadable(why);
        }
        var sha = await gitOps.GetLastCommitForPathsAsync(sandbox, RevisionPaths(lookup.Base!), cancellationToken);
        var set = SetOf(ticket, lookup.Base!, lookup.Document!, phases.Read, phases.Executed);
        logger.LogInformation(
            "Series {Base} of {Ticket} read from the ticket branch: {Phases} spec(s), revision {Revision}",
            lookup.Base, set.Key, set.Phases.Count, set.Current.Number);
        return SpecSetOnBranch.Answered(new SpecSetReadResult(set, sha));
    }

    /// <summary>The paths whose last commit is a series' revision sha.</summary>
    public static IReadOnlyList<string> RevisionPaths(string seriesBase) =>
        [SeriesPaths.Manifest(seriesBase), SeriesPaths.SpecFilesPathspec(seriesBase)];

    private async Task<(IReadOnlyList<SpecPhase> Read, IReadOnlyList<string> Executed, string? Why)> ReadSpecsAsync(
        ISandboxFileReader files, SeriesManifestDocument doc, CancellationToken ct)
    {
        var listed = await ListStatesAsync(files, ct);
        var read = new List<SpecPhase>(doc.Specs.Count);
        var executed = new List<string>();
        foreach (var id in doc.Specs)
        {
            var spec = await specs.ReadAsync(files, listed, id, doc, ct);
            if (spec.Phase is null) return ([], [], spec.Why);
            read.Add(spec.Phase);
            if (spec.Executed) executed.Add(spec.Phase.PhaseId);
        }
        return (read, executed, null);
    }

    private static async Task<IReadOnlyList<string>> ListStatesAsync(ISandboxFileReader files, CancellationToken ct) =>
        [.. await ListAsync(files, SeriesPaths.Planned, ct),
            .. await ListAsync(files, SeriesPaths.Done, ct),
            .. await ListAsync(files, SeriesPaths.Active, ct)];

    private static async Task<IReadOnlyList<string>> ListAsync(
        ISandboxFileReader files, string directory, CancellationToken ct) =>
        [.. (await files.ListAsync(directory, maxDepth: 1, ct))
            .Select(p => $"{directory}/{SeriesPaths.FileName(p)}")
            .Distinct(StringComparer.Ordinal)];

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
