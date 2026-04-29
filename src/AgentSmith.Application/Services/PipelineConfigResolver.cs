using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services;

/// <summary>
/// Merges per-pipeline overrides on top of project defaults. Implements the
/// skills-path fallback chain: pipelineDef.SkillsPath →
/// PipelinePresets.GetDefaultSkillsPath(pipelineName).
/// </summary>
public sealed class PipelineConfigResolver : IPipelineConfigResolver
{
    public ResolvedPipelineConfig Resolve(ProjectConfig project, string pipelineName)
    {
        var definition = FindPipeline(project, pipelineName)
            ?? new PipelineDefinition { Name = pipelineName };
        var agent = definition.Agent ?? project.Agent;
        var skillsPath = ResolveSkillsPath(definition, pipelineName);
        var codingPrinciplesPath = definition.CodingPrinciplesPath ?? project.CodingPrinciplesPath;
        return new ResolvedPipelineConfig(pipelineName, agent, skillsPath, codingPrinciplesPath);
    }

    public string ResolveDefaultPipelineName(ProjectConfig project)
    {
        if (!string.IsNullOrEmpty(project.DefaultPipeline))
            return project.DefaultPipeline;
        if (project.Pipelines.Count == 1)
            return project.Pipelines[0].Name;
        if (project.Pipelines.Count == 0 && !string.IsNullOrEmpty(project.Pipeline))
            return project.Pipeline;

        var declared = project.Pipelines.Count == 0
            ? "<none>"
            : string.Join(", ", project.Pipelines.ConvertAll(p => p.Name));
        throw new InvalidOperationException(
            $"Project has no default_pipeline and {project.Pipelines.Count} pipelines are declared " +
            $"(declared: {declared}); specify --pipeline or set default_pipeline.");
    }

    private static PipelineDefinition? FindPipeline(ProjectConfig project, string pipelineName)
    {
        foreach (var p in project.Pipelines)
            if (string.Equals(p.Name, pipelineName, StringComparison.OrdinalIgnoreCase))
                return p;

        return TrySynthesizeFromLegacy(project, pipelineName);
    }

    private static PipelineDefinition? TrySynthesizeFromLegacy(ProjectConfig project, string pipelineName)
    {
        if (project.Pipelines.Count > 0) return null;
        if (!string.Equals(project.Pipeline, pipelineName, StringComparison.OrdinalIgnoreCase)) return null;
        var skillsPathOverride = string.Equals(project.SkillsPath, "skills/coding", StringComparison.Ordinal)
            ? null
            : project.SkillsPath;
        return new PipelineDefinition
        {
            Name = project.Pipeline,
            SkillsPath = skillsPathOverride,
            CodingPrinciplesPath = project.CodingPrinciplesPath,
        };
    }

    private static string ResolveSkillsPath(PipelineDefinition definition, string pipelineName)
        => definition.SkillsPath ?? PipelinePresets.GetDefaultSkillsPath(pipelineName);
}
