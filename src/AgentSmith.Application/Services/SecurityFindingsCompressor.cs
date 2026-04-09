using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Compresses raw security findings into compact summaries and skill-specific slices.
/// Reduces token usage by ~74% while preserving actionable signal.
/// </summary>
public static class SecurityFindingsCompressor
{
    private const int DetailThreshold = 15;
    private const int MaxMatchedTextLength = 80;

    private static readonly Dictionary<string, string[]> SkillCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["secrets-detector"] = ["secrets", "history"],
        ["injection-checker"] = ["injection", "ssrf"],
        ["auth-reviewer"] = ["secrets", "injection"],
        ["config-auditor"] = ["config"],
        ["supply-chain-auditor"] = ["dependencies"],
        ["compliance-checker"] = ["compliance"],
        ["ai-security-reviewer"] = ["ai-security"],
        ["vuln-analyst"] = ["secrets", "injection", "ssrf", "config", "dependencies"],
        ["false-positive-filter"] = ["secrets", "injection", "ssrf", "config", "compliance", "ai-security", "dependencies", "history"],
    };

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
        {
            sb.AppendLine($"### Static Pattern Scan ({staticResult.Findings.Count} findings in {staticResult.FilesScanned} files)");
            sb.AppendLine();
            sb.AppendLine("| Category | Critical | High | Medium | Low | Total |");
            sb.AppendLine("|----------|----------|------|--------|-----|-------|");

            var byCategory = staticResult.Findings
                .GroupBy(f => f.Category, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count());

            foreach (var group in byCategory)
            {
                var c = group.Count(f => f.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase));
                var h = group.Count(f => f.Severity.Equals("high", StringComparison.OrdinalIgnoreCase));
                var m = group.Count(f => f.Severity.Equals("medium", StringComparison.OrdinalIgnoreCase));
                var l = group.Count(f => f.Severity.Equals("low", StringComparison.OrdinalIgnoreCase));
                sb.AppendLine($"| {group.Key} | {c} | {h} | {m} | {l} | {group.Count()} |");
            }

            sb.AppendLine();
        }

        if (historyResult is not null && historyResult.Findings.Count > 0)
        {
            var critical = historyResult.Findings.Count(f => !f.StillInWorkingTree);
            var high = historyResult.Findings.Count(f => f.StillInWorkingTree);
            sb.AppendLine($"### Git History Secrets ({historyResult.Findings.Count} in {historyResult.CommitsScanned} commits)");
            sb.AppendLine($"- {critical} CRITICAL (deleted from code but still in git history)");
            sb.AppendLine($"- {high} HIGH (still in working tree)");
            sb.AppendLine();
        }

        if (dependencyResult is not null && dependencyResult.Findings.Count > 0)
        {
            sb.AppendLine($"### Dependency Audit ({dependencyResult.Ecosystem})");
            foreach (var f in dependencyResult.Findings)
            {
                var cve = f.Cve is not null ? $" ({f.Cve})" : "";
                sb.AppendLine($"- {f.Severity.ToUpperInvariant()}: {f.Package} {f.Version}{cve} — {f.Title}");
            }

            sb.AppendLine();
        }

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
                slices[group.Key] = CompressPatternFindings(group.Key, group.ToList());
            }
        }

        if (historyResult is not null && historyResult.Findings.Count > 0)
        {
            slices["history"] = CompressHistoryFindings(historyResult.Findings);
        }

        if (dependencyResult is not null && dependencyResult.Findings.Count > 0)
        {
            slices["dependencies"] = CompressDependencyFindings(dependencyResult);
        }

        return slices;
    }

    /// <summary>
    /// Returns the relevant finding slice for a specific skill.
    /// </summary>
    public static string GetSliceForSkill(string skillName, Dictionary<string, string> categorySlices)
    {
        if (!SkillCategories.TryGetValue(skillName, out var categories))
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var cat in categories)
        {
            if (categorySlices.TryGetValue(cat, out var slice))
            {
                sb.AppendLine(slice);
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string CompressPatternFindings(string category, List<PatternFinding> findings)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### {category} findings ({findings.Count})");

        var sorted = findings.OrderByDescending(x => SeverityOrder(x.Severity)).ToList();

        // Full detail for small categories, compact one-liners for larger ones
        if (sorted.Count <= DetailThreshold)
        {
            foreach (var f in sorted)
            {
                var matched = TruncateMatch(f.MatchedText);
                sb.AppendLine($"- **{f.Severity.ToUpperInvariant()}** `{f.File}:{f.Line}` — {f.Title} [{matched}]");
            }
        }
        else
        {
            foreach (var f in sorted)
            {
                sb.AppendLine($"- **{f.Severity.ToUpperInvariant()}** `{f.File}:{f.Line}` — {f.Title}");
            }
        }

        return sb.ToString();
    }

    private static string CompressHistoryFindings(IReadOnlyList<HistoryFinding> findings)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### Git History Secrets ({findings.Count})");

        foreach (var f in findings.OrderByDescending(x => x.StillInWorkingTree ? 0 : 1))
        {
            var status = f.StillInWorkingTree ? "still in code" : "DELETED but in history";
            sb.AppendLine($"- **{f.Severity.ToUpperInvariant()}** `{f.File}` (commit {f.CommitHash[..7]}) — {f.Title} [{status}]");
        }

        return sb.ToString();
    }

    private static string CompressDependencyFindings(DependencyAuditResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### Dependency Vulnerabilities ({result.Ecosystem}, {result.Findings.Count})");

        foreach (var f in result.Findings)
        {
            var cve = f.Cve is not null ? $" [{f.Cve}]" : "";
            var fix = f.FixVersion is not null ? $" → fix: {f.FixVersion}" : "";
            sb.AppendLine($"- **{f.Severity.ToUpperInvariant()}** {f.Package} {f.Version}{cve} — {f.Title}{fix}");
        }

        return sb.ToString();
    }

    private static string? TruncateMatch(string? text)
    {
        if (text is null) return null;
        return text.Length > MaxMatchedTextLength
            ? text[..MaxMatchedTextLength] + "..."
            : text;
    }

    private static int SeverityOrder(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" => 4,
        "high" => 3,
        "medium" => 2,
        "low" => 1,
        _ => 0,
    };
}
