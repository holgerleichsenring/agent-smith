using System.Text.Json;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Infrastructure.Services.Zap;

/// <summary>
/// Parses ZAP JSON report output into structured findings.
/// </summary>
internal static class ZapReportParser
{
    internal static List<ZapFinding> ParseZapJson(string output)
    {
        var findings = new List<ZapFinding>();

        if (string.IsNullOrWhiteSpace(output))
            return findings;

        try
        {
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            if (!root.TryGetProperty("site", out var sites) || sites.ValueKind != JsonValueKind.Array)
                return findings;

            foreach (var site in sites.EnumerateArray())
            {
                if (!site.TryGetProperty("alerts", out var alerts) || alerts.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var alert in alerts.EnumerateArray())
                {
                    var alertRef = GetString(alert, "alertRef");
                    var name = GetString(alert, "name");
                    var riskDesc = GetString(alert, "riskdesc");
                    var confidence = GetString(alert, "confidence");
                    var desc = GetString(alert, "desc");
                    var solution = GetStringOrNull(alert, "solution");
                    var cweId = GetStringOrNull(alert, "cweid");
                    var wascId = GetStringOrNull(alert, "wascid");

                    var riskLevel = ExtractRiskLevel(riskDesc);

                    var url = "";
                    if (alert.TryGetProperty("instances", out var instances) && instances.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var inst in instances.EnumerateArray())
                        {
                            if (inst.TryGetProperty("uri", out var uri))
                            {
                                url = uri.GetString() ?? "";
                                break;
                            }
                        }
                    }

                    var countStr = GetString(alert, "count");
                    _ = int.TryParse(countStr, out var count);

                    findings.Add(new ZapFinding(alertRef, name, riskLevel, confidence, url, desc, solution, cweId, wascId, count));
                }
            }
        }
        catch
        {
            // If JSON parsing fails entirely, return empty findings
        }

        return findings;
    }

    internal static string ExtractRiskLevel(string riskDesc)
    {
        if (string.IsNullOrWhiteSpace(riskDesc))
            return "Informational";

        // ZAP riskdesc format: "Medium (High)" where first part is risk, parenthetical is confidence
        var parenIndex = riskDesc.IndexOf('(');
        var risk = parenIndex > 0 ? riskDesc[..parenIndex].Trim() : riskDesc.Trim();
        return string.IsNullOrEmpty(risk) ? "Informational" : risk;
    }

    private static string GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) ? value.GetString() ?? "" : "";

    private static string? GetStringOrNull(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) ? value.GetString() : null;
}
