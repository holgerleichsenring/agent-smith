using System.Text.Json;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Parses the 'confirmed' array of a gate response into SkillObservations.
/// Gate-emitted observations carry ReviewStatus="confirmed" by definition —
/// the gate has decided they are real findings worth keeping.
/// </summary>
internal sealed class GateObservationParser
{
    internal static List<SkillObservation> Parse(JsonElement confirmedArray, string gateRole)
    {
        var observations = new List<SkillObservation>();
        var index = 0;
        foreach (var item in confirmedArray.EnumerateArray())
        {
            observations.Add(ParseSingle(item, gateRole, index));
            index++;
        }
        return observations;
    }

    private static SkillObservation ParseSingle(JsonElement item, string role, int index)
    {
        var description = item.TryGetProperty("description", out var d)
            ? d.GetString() ?? ""
            : item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
        var rationale = item.TryGetProperty("rationale", out var r)
            ? r.GetString()
            : item.TryGetProperty("reason", out var rs) ? rs.GetString() : null;
        var suggestion = item.TryGetProperty("suggestion", out var sg) ? sg.GetString() ?? "" : "";
        var severity = ParseSeverity(item);
        var confidence = item.TryGetProperty("confidence", out var c) ? c.GetInt32() : 80;
        var concern = ParseConcern(item);
        var category = item.TryGetProperty("category", out var cat) ? NullIfEmpty(cat.GetString()) : null;
        var file = item.TryGetProperty("file", out var f) ? NullIfEmpty(f.GetString()) : null;
        var startLine = item.TryGetProperty("start_line", out var sl) ? sl.GetInt32()
            : item.TryGetProperty("line", out var l) ? l.GetInt32() : 0;
        var apiPath = item.TryGetProperty("api_path", out var ap) ? NullIfEmpty(ap.GetString())
            : item.TryGetProperty("apiPath", out var ap2) ? NullIfEmpty(ap2.GetString()) : null;
        var schemaName = item.TryGetProperty("schema_name", out var sn) ? NullIfEmpty(sn.GetString())
            : item.TryGetProperty("schemaName", out var sn2) ? NullIfEmpty(sn2.GetString()) : null;
        var evidenceMode = ParseEvidenceMode(item);

        return new SkillObservation(
            Id: index, Role: role, Concern: concern,
            Description: description, Suggestion: suggestion,
            Blocking: false, Severity: severity, Confidence: Math.Clamp(confidence, 0, 100),
            Rationale: rationale,
            File: file, StartLine: startLine,
            ApiPath: apiPath, SchemaName: schemaName,
            EvidenceMode: evidenceMode,
            ReviewStatus: "confirmed",
            Category: category);
    }

    private static ObservationSeverity ParseSeverity(JsonElement item)
    {
        if (!item.TryGetProperty("severity", out var s)) return ObservationSeverity.Medium;
        var value = s.GetString();
        if (string.IsNullOrWhiteSpace(value)) return ObservationSeverity.Medium;
        return value.Trim().ToLowerInvariant() switch
        {
            "critical" or "high" or "error" => ObservationSeverity.High,
            "medium" or "warning" or "warn" => ObservationSeverity.Medium,
            "low" or "note" => ObservationSeverity.Low,
            _ => ObservationSeverity.Info,
        };
    }

    private static ObservationConcern ParseConcern(JsonElement item)
    {
        if (item.TryGetProperty("concern", out var c)
            && Enum.TryParse<ObservationConcern>(c.GetString(), ignoreCase: true, out var parsed))
            return parsed;
        return ObservationConcern.Security;
    }

    private static EvidenceMode ParseEvidenceMode(JsonElement item)
    {
        if (item.TryGetProperty("evidence_mode", out var e)
            && Enum.TryParse<EvidenceMode>(e.GetString(), ignoreCase: true, out var parsed))
            return parsed;
        return EvidenceMode.Potential;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
