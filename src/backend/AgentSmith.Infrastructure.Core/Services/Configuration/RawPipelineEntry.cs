namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Raw YAML shape for one entry inside <c>project.pipelines:</c>.
/// Agent is a catalog reference (string name) here; ConfigCatalogResolver looks it up
/// against the agents catalog and writes the resolved <c>AgentConfig</c> into the
/// corresponding <c>PipelineDefinition</c>.
/// </summary>
public sealed class RawPipelineEntry
{
    public string Name { get; set; } = string.Empty;
    public string? Agent { get; set; }
    public string? SkillsPath { get; set; }
    public string? CodingPrinciplesPath { get; set; }
}
