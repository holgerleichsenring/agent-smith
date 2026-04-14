using System.Text.Json;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Parses pip-audit JSON output into dependency findings.
/// </summary>
internal sealed class PipAuditParser
{
    internal List<DependencyFinding> Parse(string json)
    {
        var findings = new List<DependencyFinding>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            return findings;

        foreach (var entry in root.EnumerateArray())
        {
            var name = entry.TryGetProperty("name", out var n) ? n.GetString() ?? "unknown" : "unknown";
            var version = entry.TryGetProperty("version", out var v) ? v.GetString() ?? "unknown" : "unknown";

            if (!entry.TryGetProperty("vulns", out var vulns) || vulns.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var vuln in vulns.EnumerateArray())
            {
                var id = vuln.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                var desc = vuln.TryGetProperty("description", out var descProp)
                    ? descProp.GetString() ?? "No description"
                    : "No description";

                string? fixVersion = null;
                if (vuln.TryGetProperty("fix_versions", out var fixes) && fixes.ValueKind == JsonValueKind.Array)
                {
                    var versions = new List<string>();
                    foreach (var fv in fixes.EnumerateArray())
                    {
                        var s = fv.GetString();
                        if (s is not null) versions.Add(s);
                    }

                    if (versions.Count > 0)
                        fixVersion = string.Join(", ", versions);
                }

                findings.Add(new DependencyFinding(
                    Package: name,
                    Version: version,
                    Severity: "unknown",
                    Cve: id,
                    Title: $"Vulnerability in {name} {version}",
                    Description: desc,
                    FixVersion: fixVersion,
                    Ecosystem: "python"));
            }
        }

        return findings;
    }
}
