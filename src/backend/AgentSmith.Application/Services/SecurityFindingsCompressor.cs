using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Compresses raw security findings into compact summaries and skill-specific slices.
/// Reduces token usage by ~74% while preserving actionable signal.
/// </summary>
public static class SecurityFindingsCompressor
{
    private const string Wildcard = "*";

    /// <summary>
    /// Builds a compact summary table of all findings for baseline context.
    /// </summary>
    public static string BuildSummary(
        StaticScanResult? staticResult,
        GitHistoryScanResult? historyResult,
        DependencyAuditResult? dependencyResult)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Security Scan Findings Summary");
        sb.AppendLine();

        if (staticResult is not null && staticResult.Findings.Count > 0)
            SecurityFindingsFormatter.AppendStaticScanSummary(sb, staticResult);

        if (historyResult is not null && historyResult.Findings.Count > 0)
            SecurityFindingsFormatter.AppendHistorySummary(sb, historyResult);

        if (dependencyResult is not null && dependencyResult.Findings.Count > 0)
            SecurityFindingsFormatter.AppendDependencySummary(sb, dependencyResult);

        if (sb.Length < 50)
            sb.AppendLine("No findings from automated scans.");

        return sb.ToString();
    }

    /// <summary>
    /// Builds skill-specific finding slices keyed by category name.
    /// </summary>
    public static Dictionary<string, string> BuildCategorySlices(
        StaticScanResult? staticResult,
        GitHistoryScanResult? historyResult,
        DependencyAuditResult? dependencyResult)
    {
        var slices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (staticResult is not null)
        {
            var byCategory = staticResult.Findings
                .GroupBy(f => f.Category, StringComparer.OrdinalIgnoreCase);

            foreach (var group in byCategory)
            {
                slices[group.Key] = SecurityFindingsFormatter.CompressPatternFindings(group.Key, group.ToList());
            }
        }

        if (historyResult is not null && historyResult.Findings.Count > 0)
        {
            slices["history"] = SecurityFindingsFormatter.CompressHistoryFindings(historyResult.Findings);
        }

        if (dependencyResult is not null && dependencyResult.Findings.Count > 0)
        {
            slices["dependencies"] = SecurityFindingsFormatter.CompressDependencyFindings(dependencyResult);
        }

        return slices;
    }

    /// <summary>
    /// Returns the relevant finding slice for a specific skill.
    /// Reads categories from the skill's orchestration.InputCategories — empty means
    /// the skill does not consume a category-scoped slice (typical for Executor skills
    /// that get the full commodity section elsewhere). "*" yields every slice concatenated.
    /// </summary>
    public static string GetSliceForSkill(
        string skillName,
        Dictionary<string, string> categorySlices,
        IReadOnlyList<string>? inputCategories = null)
    {
        if (inputCategories is null || inputCategories.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();

        if (inputCategories.Count == 1 && inputCategories[0] == Wildcard)
        {
            foreach (var slice in categorySlices.Values)
            {
                sb.AppendLine(slice);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        foreach (var cat in inputCategories)
        {
            if (categorySlices.TryGetValue(cat, out var slice))
            {
                sb.AppendLine(slice);
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
