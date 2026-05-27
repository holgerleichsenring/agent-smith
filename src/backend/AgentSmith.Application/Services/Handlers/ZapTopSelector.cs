using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0151g: deterministic top-N selector for ZAP findings. ZAP typically
/// emits ≤ 10 alerts per scan so the cap is generous (30). Sort by risk
/// descending, then URL + alertRef as tie-breakers — same scanner output
/// always yields the same selection.
/// </summary>
public sealed class ZapTopSelector
{
    private const int DefaultCap = 30;

    public IReadOnlyList<ZapFinding> SelectTop(
        IReadOnlyList<ZapFinding>? findings, int cap = DefaultCap)
    {
        if (findings is null || findings.Count == 0) return Array.Empty<ZapFinding>();
        return findings
            .OrderByDescending(f => RiskRank(f.RiskDescription))
            .ThenBy(f => f.Url, StringComparer.Ordinal)
            .ThenBy(f => f.AlertRef, StringComparer.Ordinal)
            .Take(cap)
            .ToArray();
    }

    private static int RiskRank(string risk) => risk?.ToLowerInvariant() switch
    {
        "high" => 4,
        "medium" => 3,
        "low" => 2,
        "informational" or "info" => 1,
        _ => 0,
    };
}
