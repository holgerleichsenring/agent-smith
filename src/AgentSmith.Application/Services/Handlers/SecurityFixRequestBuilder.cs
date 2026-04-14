using System.Globalization;
using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Static helpers that build and serialize <see cref="SecurityFixRequest"/> artifacts.
/// </summary>
internal static partial class SecurityFixRequestBuilder
{
    internal static HashSet<string> GetIncludedSeverities(string threshold) =>
        threshold.ToUpperInvariant() switch
        {
            "CRITICAL" => ["CRITICAL"],
            _ => ["CRITICAL", "HIGH"],
        };

    internal static string ExtractCategory(string title)
    {
        var firstSpace = title.IndexOf(' ');
        return firstSpace > 0 ? title[..firstSpace] : title;
    }

    internal static string? ExtractCweId(string description)
    {
        var match = CwePattern().Match(description);
        return match.Success ? match.Groups[1].Value : null;
    }

    internal static string GenerateBranchName(Finding finding)
    {
        var cweId = ExtractCweId(finding.Description);
        var slug = SlugPattern().Replace(finding.Title.ToLowerInvariant(), "-");
        slug = slug.Trim('-');
        if (slug.Length > 40) slug = slug[..40].TrimEnd('-');

        return cweId is not null
            ? $"security-fix/cwe-{cweId}-{slug}"
            : $"security-fix/{slug}";
    }

    internal static string SanitizeFileName(string branchName) =>
        branchName.Replace('/', '-');

    internal static bool IsExcluded(string filePath, List<string> excludedPatterns) =>
        excludedPatterns.Any(p => filePath.Contains(p, StringComparison.OrdinalIgnoreCase));

    internal static string SerializeFixRequest(SecurityFixRequest request)
    {
        var lines = new List<string>
        {
            $"file_path: {request.FilePath}",
            $"category: {request.Category}",
            $"suggested_branch: {request.SuggestedBranch}",
            "items:"
        };

        foreach (var item in request.Items)
        {
            lines.Add($"  - severity: {item.Severity}");
            lines.Add($"    title: \"{EscapeYaml(item.Title)}\"");
            lines.Add($"    description: \"{EscapeYaml(item.Description)}\"");
            lines.Add(item.CweId is not null
                ? $"    cwe_id: {item.CweId}"
                : "    cwe_id:");
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"    line: {item.Line}"));
        }

        return string.Join('\n', lines) + '\n';
    }

    private static string EscapeYaml(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    [GeneratedRegex(@"CWE-(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex CwePattern();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex SlugPattern();
}
