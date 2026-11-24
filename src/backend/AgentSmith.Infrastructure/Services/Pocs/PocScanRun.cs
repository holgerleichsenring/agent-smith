using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Infrastructure.Services.Pocs;

/// <summary>2026-11-23-933dc: what one PoC pattern scan collected so far — its hits, its notes, and the strict
/// failure a 'secrets' pattern's timeout sets.</summary>
internal sealed class PocScanRun
{
    public List<PocPatternHit> Hits { get; } = [];

    public List<PocScanFinding> Notes { get; } = [];

    public string? Failure { get; private set; }

    public void TimedOut(PatternDefinition pattern, string file)
    {
        if (PocPatternSet.IsSecret(pattern))
        {
            Failure ??= $"the secret pattern '{pattern.Id}' timed out on {file}; a secret there could be missed, so the "
                + "upload is not passed";
            return;
        }
        var id = "note:" + PocSecretScan.Hash($"timeout|{pattern.Id}|{file}");
        if (Notes.Any(n => n.Id == id)) return;
        Notes.Add(new PocScanFinding(id, PocScanFindingKinds.Note,
            pattern.Id, file, null, $"Pattern '{pattern.Id}' timed out on {file}",
            $"The {pattern.Category} pattern '{pattern.Id}' timed out on {file} and was skipped there; its findings are "
            + "notes, so the scan continues.", null, null, []));
    }
}
