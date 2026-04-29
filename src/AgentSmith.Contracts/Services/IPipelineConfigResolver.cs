using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Merges per-pipeline overrides on top of project-level defaults and applies
/// the skills-path fallback chain. Single source of truth for what
/// <c>Agent</c>, <c>SkillsPath</c>, <c>CodingPrinciplesPath</c> a pipeline
/// run sees.
/// </summary>
public interface IPipelineConfigResolver
{
    ResolvedPipelineConfig Resolve(ProjectConfig project, string pipelineName);

    /// <summary>
    /// Resolves the pipeline name to use when no explicit name is supplied.
    /// Chain: <c>project.DefaultPipeline</c> → single-element shortcut when
    /// only one pipeline is declared → throws when ambiguous.
    /// </summary>
    string ResolveDefaultPipelineName(ProjectConfig project);
}
