using AgentSmith.Application.Services.Pocs.Scan;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Build;

/// <summary>
/// 2026-11-23-933dd: the ONE decision of the image stage, through the code stage's rules so both decide alike — a
/// vulnerability by <see cref="PocVulnerabilityRule"/> on its highest V3Score (Trivy's severity label decides nothing),
/// with the id 'trivy:' + VulnerabilityID|package|installed; a secret by <see cref="PocSecretRule"/> with the confidence
/// of the catalog pattern its Trivy rule maps to; an unmapped rule and an advisory an overrule lifted are notes.
/// </summary>
public sealed class PocImageRules(PocVulnerabilityRule vulnerabilities, PocSecretRule secrets, PocFindingIds ids)
{
    public PocScanVerdict Decide(
        IReadOnlyList<TrivyReportEntry> entries, IReadOnlySet<string> lifted, Func<string, int?> confidenceOf, PocScanConfig config)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var judged = entries.Select(e => e.Kind == TrivyReportEntry.SecretKind
                ? Secret(e, Next(occurrences, $"{e.Id}|{e.Target}|{e.Line}"), confidenceOf(e.Id), config)
                : Vulnerability(e, lifted, config))
            .DistinctBy(f => f.Id).ToList();
        return new PocScanVerdict(
            [.. judged.Where(f => f.Kind != PocScanFindingKinds.Note)], [.. judged.Where(f => f.Kind == PocScanFindingKinds.Note)]);
    }

    private PocScanFinding Vulnerability(TrivyReportEntry e, IReadOnlySet<string> lifted, PocScanConfig config)
    {
        var found = new PocVulnerability(e.Id, e.Package ?? "?", e.Installed ?? "?", "image", e.Target,
            e.Scores.Where(s => s > 0).Select(s => (double?)s).Max(),
            e.Fixed is null ? [] : [.. e.Fixed.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)],
            e.Title ?? e.Id, []);
        var finding = vulnerabilities.Judge(found, config) with { Id = $"trivy:{e.Id}|{found.Package}|{found.Version}" };
        return finding.Kind == PocScanFindingKinds.Vulnerability && lifted.Contains(e.Id)
            ? finding with { Kind = PocScanFindingKinds.Note, Detail = finding.Detail + " Lifted by an overrule, so the gate ignored it." }
            : finding;
    }

    private PocScanFinding Secret(TrivyReportEntry e, int occurrence, int? confidence, PocScanConfig config)
    {
        var hit = new PocPatternHit(e.Id, "secrets", e.Severity.ToLowerInvariant(), confidence ?? 0, e.Target, e.Line ?? 0,
            e.Title ?? e.Id, e.Title ?? e.Id, string.Empty, null);
        var finding = secrets.Judge(hit, occurrence, config) with { MaskedSnippet = null };
        return confidence is not null ? finding : finding with
        {
            Kind = PocScanFindingKinds.Note, Fix = null,
            Detail = $"Trivy rule '{e.Id}' ({e.Severity}) maps to no catalog pattern, so it has no confidence to reach "
                + $"{PocScanConfig.SecretMinConfidenceKey} ({config.SecretMinConfidence}): reported, not blocking.",
        };
    }

    private static int Next(Dictionary<string, int> occurrences, string key)
    {
        var n = occurrences.GetValueOrDefault(key);
        occurrences[key] = n + 1;
        return n;
    }
}
