using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Formats security findings into compact markdown for skill consumption.
/// </summary>
internal static class SecurityFindingsFormatter
{
    private const int DetailThreshold = 15;
    private const int MaxMatchedTextLength = 80;

    internal static string CompressPatternFindings(string category, List<PatternFinding> findings)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### {category} findings ({findings.Count})");
        var sorted = findings.OrderByDescending(x => SeverityOrder(x.Severity)).ToList();

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

    internal static string CompressHistoryFindings(IReadOnlyList<HistoryFinding> findings)
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

    internal static string CompressDependencyFindings(DependencyAuditResult result)
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

    internal static void AppendStaticScanSummary(StringBuilder sb, StaticScanResult result)
    {
        sb.AppendLine($"### Static Pattern Scan ({result.Findings.Count} findings in {result.FilesScanned} files)");
        sb.AppendLine();
        sb.AppendLine("| Category | Critical | High | Medium | Low | Total |");
        sb.AppendLine("|----------|----------|------|--------|-----|-------|");
        var byCategory = result.Findings
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

    internal static void AppendHistorySummary(StringBuilder sb, GitHistoryScanResult result)
    {
        var critical = result.Findings.Count(f => !f.StillInWorkingTree);
        var high = result.Findings.Count(f => f.StillInWorkingTree);
        sb.AppendLine($"### Git History Secrets ({result.Findings.Count} in {result.CommitsScanned} commits)");
        sb.AppendLine($"- {critical} CRITICAL (deleted from code but still in git history)");
        sb.AppendLine($"- {high} HIGH (still in working tree)");
        sb.AppendLine();
    }

    internal static void AppendDependencySummary(StringBuilder sb, DependencyAuditResult result)
    {
        sb.AppendLine($"### Dependency Audit ({result.Ecosystem})");
        foreach (var f in result.Findings)
            sb.AppendLine($"- {f.Severity.ToUpperInvariant()}: {f.Package} {f.Version}{(f.Cve is not null ? $" ({f.Cve})" : "")} — {f.Title}");
        sb.AppendLine();
    }

    internal static int SeverityOrder(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" => 4,
        "high" => 3,
        "medium" => 2,
        "low" => 1,
        _ => 0,
    };

    private static string? TruncateMatch(string? text) =>
        text is null ? null :
        text.Length > MaxMatchedTextLength ? text[..MaxMatchedTextLength] + "..." : text;
}
