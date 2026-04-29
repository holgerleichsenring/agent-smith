using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Adds the legacy single-string Pipeline to the Pipelines list (if not already
/// declared) so it acts as a default that pipeline_from_label can route to.
/// Per-pipeline overrides go in Pipelines explicitly; pipelines without
/// overrides inherit project defaults via the resolver. Validates only the
/// project-level DefaultPipeline against the declared list — trigger label
/// values are not pre-validated since they may route to any system pipeline.
/// </summary>
public sealed class ProjectConfigNormalizer
{
    private const string DefaultProjectSkillsPath = "skills/coding";

    public void Normalize(string projectName, ProjectConfig project)
    {
        ApplyLegacyShim(project);
        ValidateDefaultPipeline(projectName, project);
    }

    private static void ApplyLegacyShim(ProjectConfig project)
    {
        if (string.IsNullOrEmpty(project.Pipeline)) return;
        if (!project.Pipelines.Any(p => string.Equals(
            p.Name, project.Pipeline, StringComparison.OrdinalIgnoreCase)))
        {
            project.Pipelines.Add(new PipelineDefinition
            {
                Name = project.Pipeline,
                SkillsPath = NonDefaultSkillsPath(project),
                CodingPrinciplesPath = project.CodingPrinciplesPath,
            });
        }
        project.DefaultPipeline ??= project.Pipeline;
    }

    private static string? NonDefaultSkillsPath(ProjectConfig project) =>
        string.Equals(project.SkillsPath, DefaultProjectSkillsPath, StringComparison.Ordinal)
            ? null
            : project.SkillsPath;

    private static void ValidateDefaultPipeline(string projectName, ProjectConfig project)
    {
        if (project.DefaultPipeline is null) return;
        if (project.Pipelines.Any(p => string.Equals(
            p.Name, project.DefaultPipeline, StringComparison.OrdinalIgnoreCase))) return;

        throw new ConfigurationException(
            $"Project '{projectName}': default_pipeline '{project.DefaultPipeline}' " +
            $"is not declared in pipelines.");
    }
}
