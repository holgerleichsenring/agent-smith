using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Synthesizes a single-element Pipelines list from the legacy
/// Pipeline + SkillsPath fields when Pipelines is empty, and validates that
/// trigger pipeline references resolve to declared pipeline names.
/// </summary>
public sealed class ProjectConfigNormalizer
{
    private const string DefaultProjectSkillsPath = "skills/coding";

    public void Normalize(string projectName, ProjectConfig project)
    {
        ApplyLegacyShim(project);
        ValidateDefaultPipeline(projectName, project);
        ValidateTriggers(projectName, project);
    }

    private static void ApplyLegacyShim(ProjectConfig project)
    {
        if (project.Pipelines.Count > 0) return;
        if (string.IsNullOrEmpty(project.Pipeline)) return;

        var skillsPathOverride = string.Equals(
            project.SkillsPath, DefaultProjectSkillsPath, StringComparison.Ordinal)
            ? null
            : project.SkillsPath;

        project.Pipelines.Add(new PipelineDefinition
        {
            Name = project.Pipeline,
            SkillsPath = skillsPathOverride,
            CodingPrinciplesPath = project.CodingPrinciplesPath,
        });
        project.DefaultPipeline ??= project.Pipeline;
    }

    private static void ValidateDefaultPipeline(string projectName, ProjectConfig project)
    {
        if (project.DefaultPipeline is null) return;
        if (project.Pipelines.Any(p => string.Equals(
            p.Name, project.DefaultPipeline, StringComparison.OrdinalIgnoreCase))) return;

        throw new ConfigurationException(
            $"Project '{projectName}': default_pipeline '{project.DefaultPipeline}' " +
            $"is not declared in pipelines.");
    }

    private static void ValidateTriggers(string projectName, ProjectConfig project)
    {
        if (project.Pipelines.Count == 0) return;
        var declared = new HashSet<string>(
            project.Pipelines.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);

        ValidateTrigger(projectName, "github_trigger", project.GithubTrigger, declared);
        ValidateTrigger(projectName, "gitlab_trigger", project.GitlabTrigger, declared);
        ValidateTrigger(projectName, "azuredevops_trigger", project.AzuredevopsTrigger, declared);
        ValidateTrigger(projectName, "jira_trigger", project.JiraTrigger, declared);
    }

    private static void ValidateTrigger(
        string projectName, string triggerName, WebhookTriggerConfig? trigger,
        HashSet<string> declared)
    {
        if (trigger is null) return;

        foreach (var (label, pipeline) in trigger.PipelineFromLabel)
            if (!declared.Contains(pipeline))
                throw new ConfigurationException(
                    $"Project '{projectName}' {triggerName}: pipeline_from_label['{label}'] " +
                    $"references unknown pipeline '{pipeline}'.");

        if (!string.IsNullOrEmpty(trigger.DefaultPipeline) && !declared.Contains(trigger.DefaultPipeline))
            throw new ConfigurationException(
                $"Project '{projectName}' {triggerName}: default_pipeline " +
                $"'{trigger.DefaultPipeline}' is not declared in pipelines.");
    }
}
