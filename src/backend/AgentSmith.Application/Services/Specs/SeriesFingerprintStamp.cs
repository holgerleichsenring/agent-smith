using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-17-dd05b: after this system rewrote a ticket's body from its branch, the manifest takes the fingerprint of
/// the ticket as the tracker now stores it — otherwise the next run compares the old one and reads the rewrite as a
/// person's edit. Only the manifests are written, so no spec file moves, and the pointer records the new commits, so the
/// write is not read as a reviewer's edit either.
/// </summary>
public sealed class SeriesFingerprintStamp(
    ISourceProviderFactory sources, SeriesFiles files, SeriesRepos placer, ISpecSetPointerStore stored, SpecSetPointerRecorder pointers)
{
    /// <summary>Null when the manifest carries the ticket's fingerprint now; else why it could not be written.</summary>
    /// <param name="set">The set as read from the ticket branch.</param>
    /// <param name="ticket">The ticket as the tracker stores it after the rewrite.</param>
    public async Task<string?> StampAsync(ResolvedProject project, SpecSet set, Ticket ticket, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(ticket);
        var fingerprint = TicketTextFingerprint.Of(ticket);
        if (string.Equals(fingerprint, set.TicketFingerprint, StringComparison.Ordinal)) return null;
        var pointer = await stored.GetAsync(project.Name, set.Key, ct);
        var placement = placer.Place(set with { TicketFingerprint = fingerprint }, new SeriesScope(
            project.Repos, SeriesScope.LegacyOf(pointer, null, project.Repos), [], pointer?.CarryingRepo));
        if (placement.Refusal is { } refused) return refused;
        var shas = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var repo in placement.Members)
        {
            var manifest = files.Manifest(placement.Set.OfRepository(repo.Name));
            var written = await sources.Create(repo).WriteFilesToBranchAsync(TicketBranchNamer.Compose(ticket.Id),
                [new RepoFile(manifest.Path, manifest.Content)], $"spec: {set.Series} ({set.Key}) ticket fingerprint after its body was rendered", ct);
            if (!written.Written) return $"{repo.Name}: {written.Error ?? "the write named no commit"}";
            shas[repo.Name] = written.CommitSha!;
        }
        await pointers.RecordAsync(project.Name, placement.Carrier?.Name, placement.Set, shas, ct);
        return null;
    }
}
