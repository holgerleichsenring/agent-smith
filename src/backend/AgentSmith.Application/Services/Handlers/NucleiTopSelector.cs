using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0151g: deterministic top-N selector for Nuclei findings. Sort order:
/// <list type="bullet">
///   <item>severity rank descending (critical > high > medium > low > info / unknown)</item>
///   <item>matched URL ascending (stable tie-breaker for human-readable diffs)</item>
///   <item>template id ascending (final tie-breaker)</item>
/// </list>
/// Cap defaults to 20. Same input always yields the same output.
/// </summary>
public sealed class NucleiTopSelector
{
    private const int DefaultCap = 20;

    public IReadOnlyList<NucleiFinding> SelectTop(
        IReadOnlyList<NucleiFinding>? findings, int cap = DefaultCap)
    {
        if (findings is null || findings.Count == 0) return Array.Empty<NucleiFinding>();
        return findings
            .OrderByDescending(f => SeverityRank(f.Severity))
            .ThenBy(f => f.MatchedUrl, StringComparer.Ordinal)
            .ThenBy(f => f.TemplateId, StringComparer.Ordinal)
            .Take(cap)
            .ToArray();
    }

    private static int SeverityRank(string severity) => severity?.ToLowerInvariant() switch
    {
        "critical" => 5,
        "high" => 4,
        "medium" => 3,
        "low" => 2,
        "info" or "informational" => 1,
        _ => 0,
    };
}
