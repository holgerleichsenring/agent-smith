using System.Text.Json.Serialization;

namespace AgentSmith.Application.Services.Specs.Reports;

/// <summary>2026-11-22-96c8a: what one structured read takes off a coding master's report. Every list is present and
/// empty when the report named nothing of the kind — no nullable arrays, so the schema is strict-compatible.</summary>
public sealed class MasterReportExtract
{
    [JsonPropertyName("follow_ups")] public List<ExtractedFollowUp> FollowUps { get; set; } = [];
    [JsonPropertyName("ignored_instructions")] public List<ExtractedIgnoredInstruction> IgnoredInstructions { get; set; } = [];
    [JsonPropertyName("renamed")] public List<string> Renamed { get; set; } = [];
    [JsonPropertyName("moved")] public List<string> Moved { get; set; } = [];
    [JsonPropertyName("persisted")] public List<string> Persisted { get; set; } = [];
    [JsonPropertyName("deviations")] public List<string> Deviations { get; set; } = [];
    [JsonPropertyName("relied_on")] public List<string> ReliedOn { get; set; } = [];
}

/// <summary>One proposed follow-up spec.</summary>
public sealed class ExtractedFollowUp
{
    [JsonPropertyName("goal")] public string Goal { get; set; } = string.Empty;
    [JsonPropertyName("why")] public string Why { get; set; } = string.Empty;
}

/// <summary>One ticket instruction the report says was refused.</summary>
public sealed class ExtractedIgnoredInstruction
{
    [JsonPropertyName("quote")] public string Quote { get; set; } = string.Empty;
    [JsonPropertyName("reason")] public string Reason { get; set; } = string.Empty;
}

/// <summary>The read's outcome: the extract, or why there is none.</summary>
public sealed record MasterReportRead(MasterReportExtract? Extract, string? Problem)
{
    public static MasterReportRead Failed(string problem) => new(null, problem);
}
