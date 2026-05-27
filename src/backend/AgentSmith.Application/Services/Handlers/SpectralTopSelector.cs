using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0151g: deterministic top-N selector for Spectral findings. Errors-only
/// (warnings are too noisy at API scale — the baseline reference target
/// produced ~1k warnings, mostly missing-response-code), then grouped by
/// rule code so a single chatty rule cannot crowd out everything else.
/// For each rule cluster, at most 3 representative instances. Total cap
/// of 30 entries.
/// </summary>
public sealed class SpectralTopSelector
{
    private const int InstancesPerRule = 3;
    private const int DefaultCap = 30;

    public IReadOnlyList<SpectralFinding> SelectTop(
        IReadOnlyList<SpectralFinding>? findings, int cap = DefaultCap)
    {
        if (findings is null || findings.Count == 0) return Array.Empty<SpectralFinding>();
        return findings
            .Where(IsError)
            .GroupBy(f => f.Code, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .SelectMany(g => g
                .OrderBy(f => f.Path, StringComparer.Ordinal)
                .ThenBy(f => f.Line)
                .Take(InstancesPerRule))
            .Take(cap)
            .ToArray();
    }

    private static bool IsError(SpectralFinding finding) =>
        string.Equals(finding.Severity, "error", StringComparison.OrdinalIgnoreCase);
}
