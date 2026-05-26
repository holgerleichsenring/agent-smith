using System.Text.Json;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// p0151c: Projects the pipeline's running list of <see cref="SkillObservation"/>
/// records into a compact JSON array for inclusion in a downstream skill's
/// prompt. Anchor-bearing fields (file / start_line / api_path / schema_name)
/// are preserved so the downstream skill can confirm or extend a prior
/// finding with a single targeted tool call instead of full re-discovery.
/// Long-form fields (rationale, suggestion, details) are omitted to keep
/// token cost predictable at pipeline scale.
/// </summary>
public sealed class ObservationBusProjector
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public string Project(IReadOnlyList<SkillObservation> observations)
    {
        if (observations.Count == 0) return "[]";
        var entries = observations.Select(Project).ToList();
        return JsonSerializer.Serialize(entries, Options);
    }

    private static ProjectedObservation Project(SkillObservation o) => new(
        Id: o.Id,
        Role: o.Role,
        Concern: o.Concern.ToString().ToLowerInvariant(),
        Severity: o.Severity.ToString().ToLowerInvariant(),
        EvidenceMode: ToSnake(o.EvidenceMode),
        File: NullIfEmpty(o.File),
        StartLine: o.StartLine == 0 ? null : o.StartLine,
        ApiPath: NullIfEmpty(o.ApiPath),
        SchemaName: NullIfEmpty(o.SchemaName),
        Description: o.Description);

    private static string ToSnake(EvidenceMode mode) => mode switch
    {
        EvidenceMode.AnalyzedFromSource => "analyzed_from_source",
        EvidenceMode.Confirmed => "confirmed",
        _ => "potential",
    };

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed record ProjectedObservation(
        int Id, string Role, string Concern, string Severity, string EvidenceMode,
        string? File, int? StartLine, string? ApiPath, string? SchemaName, string Description);
}
