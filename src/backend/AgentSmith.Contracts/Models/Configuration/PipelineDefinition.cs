namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Per-pipeline configuration within a project, post-catalog-resolution.
/// Optional fields override the project-level defaults; null means inherit.
/// AgentName is the original catalog reference (kept for diagnostics + validator
/// error messages). Agent is the catalog-resolved AgentConfig — both are populated
/// together by <c>ConfigCatalogResolver</c> at load time. Resolved into a
/// <see cref="ResolvedPipelineConfig"/> via <c>IPipelineConfigResolver</c>.
/// </summary>
public sealed class PipelineDefinition
{
    public string Name { get; set; } = string.Empty;
    public string? AgentName { get; set; }
    public AgentConfig? Agent { get; set; }
    public string? SkillsPath { get; set; }
    public string? CodingPrinciplesPath { get; set; }

    /// <summary>p0167b: optional override for the blocking-downgrade confidence
    /// threshold (0-100). Null means the default (70).</summary>
    public int? ConfidenceThreshold { get; set; }
}
