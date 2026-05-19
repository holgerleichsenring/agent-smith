namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// DTO matching the LLM's JSON output for a single skill observation. Skills
/// emit typed location fields (`file`, `start_line`, `end_line`, `api_path`,
/// `schema_name`) directly per the observation schema contract; no legacy
/// `location` string is parsed. Snake-case JSON binding is configured on the
/// deserializer options in <see cref="ObservationParser"/>.
///
/// Enum-shaped fields are deserialized as strings, not as typed enums, so an
/// LLM that emits a value outside the canonical set (a hallucinated
/// `evidence_mode` from a judge skill, a `concern: "summary"` from a
/// filter-summary row) does NOT abort the whole observation parse. The
/// tolerant string→enum mapping lives in <see cref="ObservationNormalizer"/>
/// where the per-role warning channel exists.
/// </summary>
internal sealed class RawObservation
{
    public string? Concern { get; set; }
    public string Description { get; set; } = "";
    public string? Suggestion { get; set; }
    public bool Blocking { get; set; }
    public string? Severity { get; set; }
    public int Confidence { get; set; }
    public string? Rationale { get; set; }
    public string? Effort { get; set; }
    public string? File { get; set; }
    public int StartLine { get; set; }
    public int? EndLine { get; set; }
    public string? ApiPath { get; set; }
    public string? SchemaName { get; set; }
    public string? EvidenceMode { get; set; }
    public string? ReviewStatus { get; set; }
    public string? Category { get; set; }
    public string? Details { get; set; }

    internal RawObservationFields ToFields() => new(
        Concern, Description, Suggestion, Blocking, Severity, Confidence,
        Rationale, Effort, File, StartLine, EndLine, ApiPath, SchemaName,
        EvidenceMode, ReviewStatus, Category, Details);
}
