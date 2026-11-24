using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Scan;

/// <summary>
/// 2026-11-23-933dc: the ONE decision of a PoC's code scan — every pattern hit through <see cref="PocSecretRule"/>,
/// every dependency vulnerability through <see cref="PocVulnerabilityRule"/>, the scans' own notes kept as notes. It
/// takes no explanation: what the agent writes about a verdict can never change it.
/// </summary>
public sealed class PocBlockRules(PocSecretRule secrets, PocVulnerabilityRule vulnerabilities)
{
    public PocScanVerdict Decide(
        IReadOnlyList<PocPatternHit> hits, IReadOnlyList<PocVulnerability> found, IReadOnlyList<PocScanFinding> notes,
        PocScanConfig config)
    {
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(found);
        ArgumentNullException.ThrowIfNull(notes);
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var judged = hits.Select(h => secrets.Judge(h, Next(occurrences, $"{h.PatternId}|{h.File}|{h.Line}"), config))
            .Concat(found.Select(v => vulnerabilities.Judge(v, config)))
            .DistinctBy(f => f.Id)
            .ToList();
        return new PocScanVerdict(
            [.. judged.Where(f => f.Kind != PocScanFindingKinds.Note)],
            [.. judged.Where(f => f.Kind == PocScanFindingKinds.Note), .. notes]);
    }

    private static int Next(Dictionary<string, int> occurrences, string key)
    {
        var n = occurrences.GetValueOrDefault(key);
        occurrences[key] = n + 1;
        return n;
    }
}
