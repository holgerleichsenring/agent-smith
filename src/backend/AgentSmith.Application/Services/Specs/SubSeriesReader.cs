using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7b: reads ONE repository's sub-series of a ticket — what the series reader did for
/// the carrying repository alone until a ticket's specs could lie in several. One sandbox per
/// repository; the manifest found by ticket key lists its spec ids, each read across planned/,
/// done/ and active/ of the same checkout (2026-10-06-03c7d/e).
/// </summary>
public sealed class SubSeriesReader(
    ISandboxFileReaderFactory readerFactory,
    SandboxGitOperations gitOps,
    SeriesManifestFinder finder,
    SeriesSpecFileReader specs,
    SeriesManifest manifest,
    SandboxTargets sandboxTargets)
{
    public async Task<SubSeriesRead> ReadAsync(
        PipelineContext pipeline, RepoConnection repo, TicketKey ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var name = repo.Name ?? string.Empty;
        var matches = sandboxTargets.SandboxesForRepo(pipeline, repo);
        if (matches.Count == 0) return new SubSeriesRead(name, SubSeriesState.NotCheckedOut);

        var sandbox = matches[0].Value;
        var files = readerFactory.Create(sandbox);
        var lookup = await finder.FindAsync(files, ticket, cancellationToken);
        if (lookup.Otherwise is { } answer)
            return answer.State == SpecSetBranchState.Unreadable
                ? SubSeriesRead.Failed(name, answer.Why ?? "the series did not read back")
                : new SubSeriesRead(name, SubSeriesState.Nothing);
        var specs = await ReadSpecsAsync(files, lookup.Document!, cancellationToken);
        if (specs.Why is { } why) return SubSeriesRead.Failed(name, why);
        return new SubSeriesRead(name, SubSeriesState.Found)
        {
            Base = lookup.Base!, Document = lookup.Document, Specs = specs.Read, Executed = specs.Executed,
            Sha = await gitOps.GetLastCommitForPathsAsync(sandbox, SeriesReader.RevisionPaths(lookup.Base!), cancellationToken),
            Stamp = manifest.CurrentRevisionTimeOf(lookup.Document!),
        };
    }

    private async Task<(IReadOnlyList<SpecPhase> Read, IReadOnlyList<string> Executed, string? Why)> ReadSpecsAsync(
        ISandboxFileReader files, SeriesManifestDocument doc, CancellationToken ct)
    {
        var listed = await ListStatesAsync(files, ct);
        var read = new List<SpecPhase>(doc.Specs.Count);
        var executed = new List<string>();
        foreach (var id in doc.Specs)
        {
            var spec = await specs.ReadAsync(files, listed, id, doc, ct);
            if (spec.Spec is null) return ([], [], spec.Why);
            read.Add(spec.Spec);
            if (spec.Executed) executed.Add(spec.Spec.PhaseId);
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
}
