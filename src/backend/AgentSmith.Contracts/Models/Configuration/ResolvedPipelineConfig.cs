namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Merged view of a project + pipeline override. All fields are populated from
/// the project's base or the pipeline's override; <see cref="Agent"/> and
/// <see cref="SkillsPath"/> are non-null after resolution.
/// p0167b: <see cref="ConfidenceThreshold"/> gates the blocking-downgrade rule
/// (a blocking observation with confidence below the threshold becomes
/// non-blocking) — per-pipeline configurable, default 70.
/// </summary>
public sealed record ResolvedPipelineConfig(
    string PipelineName,
    AgentConfig Agent,
    string SkillsPath,
    string? CodingPrinciplesPath,
    int ConfidenceThreshold = ResolvedPipelineConfig.DefaultConfidenceThreshold)
{
    public const int DefaultConfidenceThreshold = 70;
}
