using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>
/// 2026-11-23-933db: the different-app guard. It asks "is this really the same PoC?" when more than
/// pocs.archive.mismatch_ratio_percent of the files differ AND more than pocs.archive.mismatch_file_limit do — a share
/// alone trips on a tiny app, a count alone on node_modules — or when the EXPOSE port or the ENTRYPOINT/CMD changed. A
/// changed base image alone never asks. A PoC with no validated revision has nothing to protect: same.
/// </summary>
public static class PocIdentityGuard
{
    public static PocMatch Compare(PocManifest? previous, int? previousPort, string? previousEntrypoint,
        IReadOnlyList<PocArchiveEntry> next, PocDockerfile? nextDockerfile, PocArchiveLimits limits)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(limits);
        if (previous is null) return new PocMatch(next.Count, 0, 0, 0, 0, false, false, PocMatchVerdicts.Same);
        var (added, changed, unchanged) = Counted(previous, next);
        var removed = previous.Files.Count - changed - unchanged;
        var differing = added + changed + removed;
        var of = Math.Max(previous.Files.Count, next.Count);
        var ratio = of == 0 ? 0 : (int)(differing * 100L / of);
        var portChanged = previousPort is not null && previousPort != nextDockerfile?.Port;
        var entrypointChanged = !string.Equals(previousEntrypoint, nextDockerfile?.Entrypoint, StringComparison.Ordinal);
        var tooDifferent = differing * 100L > (long)limits.MismatchRatioPercent * of && differing > limits.MismatchFileLimit;
        var verdict = tooDifferent || portChanged || entrypointChanged ? PocMatchVerdicts.Ask : PocMatchVerdicts.Same;
        return new PocMatch(added, changed, removed, unchanged, ratio, portChanged, entrypointChanged, verdict);
    }

    private static (int Added, int Changed, int Unchanged) Counted(PocManifest previous, IReadOnlyList<PocArchiveEntry> next)
    {
        int added = 0, changed = 0, unchanged = 0;
        foreach (var entry in next)
        {
            if (!previous.Files.TryGetValue(entry.Path, out var was)) added++;
            else if (was.Crc32 == entry.Crc32 && was.Size == entry.Size) unchanged++;
            else changed++;
        }
        return (added, changed, unchanged);
    }
}
