namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Merged view of a project + pipeline override. All fields are populated from
/// the project's base or the pipeline's override; <see cref="Agent"/> and
/// <see cref="SkillsPath"/> are non-null after resolution.
/// </summary>
public sealed record ResolvedPipelineConfig(
    string PipelineName,
    AgentConfig Agent,
    string SkillsPath,
    string? CodingPrinciplesPath);
