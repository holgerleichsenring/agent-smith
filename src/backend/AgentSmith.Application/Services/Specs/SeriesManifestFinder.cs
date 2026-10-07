using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-06-03c7d: finds the manifest naming a ticket key among <c>series/</c> on the branch.
/// The base is the manifest's file name; the database row only caches it.
/// <para>
/// Two manifests naming one ticket are refused, never chosen between. A manifest that does not
/// parse is this ticket's broken manifest when its text names the key — an edit gone wrong, which
/// is unreadable rather than absent — and another ticket's otherwise, which is skipped.
/// </para>
/// </summary>
public sealed class SeriesManifestFinder(SeriesManifest manifest, ILogger<SeriesManifestFinder> logger)
{
    public async Task<SeriesManifestLookup> FindAsync(
        ISandboxFileReader files, TicketKey ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        var found = new List<SeriesManifestLookup>();
        var broken = new List<string>();
        foreach (var name in await ManifestNamesAsync(files, cancellationToken))
        {
            var path = $"{SeriesPaths.SeriesRoot}/{name}";
            var text = await files.TryReadAsync(path, cancellationToken);
            var doc = manifest.Parse(text);
            if (doc is null && text?.Contains(ticket.Value, StringComparison.Ordinal) == true) broken.Add(path);
            else if (doc is not null && string.Equals(doc.Ticket, ticket.Value, StringComparison.Ordinal))
                found.Add(SeriesManifestLookup.Found(SeriesPaths.StemOf(name), doc));
        }
        return Decide(ticket, found, broken);
    }

    private SeriesManifestLookup Decide(
        TicketKey ticket, IReadOnlyList<SeriesManifestLookup> found, IReadOnlyList<string> broken)
    {
        if (found.Count == 1 && broken.Count == 0) return found[0];
        if (found.Count + broken.Count == 0) return SeriesManifestLookup.Not(SpecSetOnBranch.Nothing);
        var why = broken.Count > 0
            ? $"{string.Join(", ", broken)} names ticket {ticket.Value} and did not parse"
            : $"{found.Count} manifests under {SeriesPaths.SeriesRoot}/ name ticket {ticket.Value}: "
              + string.Join(", ", found.Select(f => SeriesPaths.Manifest(f.Base!)));
        logger.LogWarning("The series of ticket {Ticket} is unreadable: {Why}", ticket.Value, why);
        return SeriesManifestLookup.Not(SpecSetOnBranch.Unreadable(why));
    }

    private static async Task<IReadOnlyList<string>> ManifestNamesAsync(
        ISandboxFileReader files, CancellationToken ct) =>
        [.. (await files.ListAsync(SeriesPaths.SeriesRoot, maxDepth: 1, ct))
            .Select(SeriesPaths.FileName)
            .Where(SeriesPaths.IsSpecFile)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
}
