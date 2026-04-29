namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Per-pipeline configuration within a project. Optional fields override the
/// project-level defaults; null means inherit. Resolved into a
/// <see cref="ResolvedPipelineConfig"/> via <c>IPipelineConfigResolver</c>.
/// </summary>
public sealed class PipelineDefinition
{
    public string Name { get; set; } = string.Empty;
    public AgentConfig? Agent { get; set; }
    public string? SkillsPath { get; set; }
    public string? CodingPrinciplesPath { get; set; }
}
