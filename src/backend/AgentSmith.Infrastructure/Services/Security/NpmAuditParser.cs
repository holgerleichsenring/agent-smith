using System.Text.Json;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Parses npm audit JSON output into dependency findings.
/// </summary>
internal sealed class NpmAuditParser
{
    internal List<DependencyFinding> Parse(string json)
    {
        var findings = new List<DependencyFinding>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("vulnerabilities", out var vulns))
            return findings;

        foreach (var vuln in vulns.EnumerateObject())
        {
            var packageName = vuln.Name;
            var detail = vuln.Value;

            var severity = detail.TryGetProperty("severity", out var sev)
                ? sev.GetString() ?? "unknown"
                : "unknown";

            var fixAvailable = detail.TryGetProperty("fixAvailable", out var fix)
                && fix.ValueKind == JsonValueKind.True;

            var title = $"Vulnerable dependency: {packageName}";
            var description = fixAvailable ? "Fix available" : "No fix available";

            string? cve = null;
            if (detail.TryGetProperty("via", out var via) && via.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in via.EnumerateArray())
                {
                    if (entry.ValueKind == JsonValueKind.Object
                        && entry.TryGetProperty("url", out var url))
                    {
                        var urlStr = url.GetString();
                        if (urlStr?.Contains("CVE-", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            cve = CveExtractor.Extract(urlStr);
                            break;
                        }
                    }

                    if (entry.ValueKind == JsonValueKind.Object
                        && entry.TryGetProperty("title", out var titleProp))
                    {
                        title = titleProp.GetString() ?? title;
                    }
                }
            }

            findings.Add(new DependencyFinding(
                Package: packageName,
                Version: "current",
                Severity: severity,
                Cve: cve,
                Title: title,
                Description: description,
                FixVersion: fixAvailable ? "latest" : null,
                Ecosystem: "npm"));
        }

        return findings;
    }
}
