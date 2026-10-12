using System.Globalization;
using AgentSmith.Contracts.Models.Structure;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-13-2d0c: the baseline's '# measured with:' line — the Limits and tolerance_percent its
/// entries were measured against. A refresh compares a changed limit with THESE, never with the
/// principles text, which the operator may already have edited by hand before the refresh ran.
/// </summary>
public sealed class StructureBaselineHeader
{
    private const string Prefix = "# measured with:";

    public string Render(StructureLimitSet limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        var metrics = limits.ByMetric.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value.ToString(CultureInfo.InvariantCulture)}");
        var tolerance = $"{StructureLimits.ToleranceKey}={limits.Tolerance.ToString(CultureInfo.InvariantCulture)}";
        return $"{Prefix} {string.Join(' ', metrics.Append(tolerance))}";
    }

    /// <summary>The recorded limits, or null when the baseline carries none (written before 2d0c).</summary>
    public StructureLimitSet? Read(string? text)
    {
        var line = (text ?? string.Empty).Split('\n').Select(l => l.TrimEnd('\r'))
            .FirstOrDefault(l => l.StartsWith(Prefix, StringComparison.Ordinal));
        if (line is null) return null;
        var metrics = new Dictionary<string, int>(StringComparer.Ordinal);
        var tolerance = 0;
        foreach (var pair in line[Prefix.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (pair.Split('=', 2) is not [var key, var raw]
                || !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) continue;
            if (key == StructureLimits.ToleranceKey && value >= 0) tolerance = value;
            else if (StructureMetric.All.Contains(key) && value > 0) metrics[key] = value;
        }
        return metrics.Count == 0 ? null : new StructureLimitSet(metrics, tolerance);
    }
}
