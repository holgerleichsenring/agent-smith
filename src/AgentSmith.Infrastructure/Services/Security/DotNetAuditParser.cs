using System.Text.Json;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Parses dotnet list package --vulnerable JSON output into dependency findings.
/// </summary>
internal sealed class DotNetAuditParser
{
    internal List<DependencyFinding> Parse(string json)
    {
        var findings = new List<DependencyFinding>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("projects", out var projects))
            return findings;

        foreach (var project in projects.EnumerateArray())
        {
            if (!project.TryGetProperty("frameworks", out var frameworks))
                continue;

            foreach (var framework in frameworks.EnumerateArray())
            {
                if (!framework.TryGetProperty("topLevelPackages", out var packages))
                    continue;

                foreach (var package in packages.EnumerateArray())
                {
                    ParsePackageVulnerabilities(package, findings);
                }
            }
        }

        return findings;
    }

    private static void ParsePackageVulnerabilities(
        JsonElement package, List<DependencyFinding> findings)
    {
        var name = package.TryGetProperty("id", out var id)
            ? id.GetString() ?? "unknown" : "unknown";
        var version = package.TryGetProperty("resolvedVersion", out var ver)
            ? ver.GetString() ?? "unknown" : "unknown";

        if (!package.TryGetProperty("vulnerabilities", out var vulns))
            return;

        foreach (var vuln in vulns.EnumerateArray())
        {
            var severity = vuln.TryGetProperty("severity", out var sev)
                ? sev.GetString() ?? "unknown" : "unknown";
            var advisoryUrl = vuln.TryGetProperty("advisoryurl", out var url)
                ? url.GetString() : null;

            findings.Add(new DependencyFinding(
                Package: name,
                Version: version,
                Severity: severity,
                Cve: advisoryUrl is not null ? CveExtractor.Extract(advisoryUrl) : null,
                Title: $"Vulnerable package: {name} {version}",
                Description: advisoryUrl ?? "See advisory for details",
                FixVersion: null,
                Ecosystem: "dotnet"));
        }
    }
}
