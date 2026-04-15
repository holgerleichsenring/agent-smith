using System.Text.Json;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Parses confirmed findings from a gate LLM response JSON array.
/// </summary>
internal sealed class GateFindingParser
{
    internal static List<Finding> Parse(JsonElement confirmedArray)
    {
        var findings = new List<Finding>();

        foreach (var item in confirmedArray.EnumerateArray())
        {
            findings.Add(ParseSingleFinding(item));
        }

        return findings;
    }

    private static Finding ParseSingleFinding(JsonElement item)
    {
        var file = item.TryGetProperty("file", out var f) ? f.GetString() ?? "" : "";
        var line = item.TryGetProperty("line", out var l) ? l.GetInt32() : 0;
        var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
        var severity = item.TryGetProperty("severity", out var s) ? s.GetString() ?? "MEDIUM" : "MEDIUM";
        var reason = item.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";
        var apiPath = item.TryGetProperty("apiPath", out var ap) ? NullIfEmpty(ap.GetString()) : null;
        var schemaName = item.TryGetProperty("schemaName", out var sn) ? NullIfEmpty(sn.GetString()) : null;
        var category = item.TryGetProperty("category", out var cat) ? cat.GetString() ?? "unknown" : "unknown";

        return new Finding(
            Severity: severity.ToUpperInvariant(),
            File: file,
            StartLine: line,
            EndLine: null,
            Title: title,
            Description: reason,
            Confidence: 8,
            ReviewStatus: "confirmed",
            ApiPath: apiPath,
            SchemaName: schemaName,
            Category: category);
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
