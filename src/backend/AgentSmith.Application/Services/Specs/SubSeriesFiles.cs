using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-17-a0b6a: reads ONE repository's sub-series of a ticket from whatever file source it is
/// handed — a run's checkout or the ticket branch on the host. The manifest is found by ticket key
/// and every spec it lists is read across planned/, done/ and active/ of the same source. The
/// revision sha is the caller's: git knows it in a checkout, the pointer on the host.
/// </summary>
public sealed class SubSeriesFiles(
    SeriesManifestFinder finder,
    SeriesSpecFileReader specs,
    SeriesManifest manifest)
{
    public async Task<SubSeriesRead> ReadAsync(
        ISandboxFileReader files, string repo, TicketKey ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        var lookup = await finder.FindAsync(files, ticket, cancellationToken);
        if (lookup.Otherwise is { } answer)
            return answer.State == SpecSetBranchState.Unreadable
                ? SubSeriesRead.Failed(repo, answer.Why ?? "the series did not read back")
                : new SubSeriesRead(repo, SubSeriesState.Nothing);
        var read = await ReadSpecsAsync(files, lookup.Document!, cancellationToken);
        if (read.Why is { } why) return SubSeriesRead.Failed(repo, why);
        return new SubSeriesRead(repo, SubSeriesState.Found)
        {
            Base = lookup.Base!, Document = lookup.Document, Specs = read.Read, Executed = read.Executed,
            Stamp = manifest.CurrentRevisionTimeOf(lookup.Document!),
        };
    }

    private async Task<(IReadOnlyList<DerivedSpec> Read, IReadOnlyList<string> Executed, string? Why)> ReadSpecsAsync(
        ISandboxFileReader files, SeriesManifestDocument doc, CancellationToken ct)
    {
        var listed = await ListStatesAsync(files, ct);
        var read = new List<DerivedSpec>(doc.Specs.Count);
        var executed = new List<string>();
        foreach (var id in doc.Specs)
        {
            var spec = await specs.ReadAsync(files, listed, id, doc, ct);
            if (spec.Spec is null) return ([], [], spec.Why);
            read.Add(spec.Spec);
            if (spec.Executed) executed.Add(spec.Spec.SpecId);
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
