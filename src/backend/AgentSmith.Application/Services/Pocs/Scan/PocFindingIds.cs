using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Scan;

/// <summary>
/// 2026-11-23-933dc: the stable ids of PoC scan findings. A secret is 'sec:' + 16 hex of sha256(rule|file|line) — a
/// second match of one rule on one line adds its ordinal — so a re-scan of the same revision names the same finding. A
/// vulnerability is 'osv:' + advisory|package|version.
/// </summary>
public sealed class PocFindingIds
{
    public string Secret(PocPatternHit hit, int occurrence)
    {
        ArgumentNullException.ThrowIfNull(hit);
        var key = $"{hit.PatternId}|{hit.File}|{hit.Line}" + (occurrence > 0 ? $"|{occurrence}" : string.Empty);
        return "sec:" + Hash(key);
    }

    public string Vulnerability(PocVulnerability vulnerability)
    {
        ArgumentNullException.ThrowIfNull(vulnerability);
        return $"osv:{vulnerability.AdvisoryId}|{vulnerability.Package}|{vulnerability.Version}";
    }

    public string Note(string key) => "note:" + Hash(key);

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
}
