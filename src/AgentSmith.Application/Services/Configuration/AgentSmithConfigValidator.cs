using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// Runs after the catalog resolver. Catches cross-cutting mistakes that the
/// resolver alone cannot see: pipeline_triggers pointing at unknown pipelines,
/// project trigger blocks mismatched against their tracker's type.
///
/// Returns all errors in one pass — composition roots aggregate and abort
/// startup if the list is non-empty.
/// </summary>
public sealed class AgentSmithConfigValidator
{
    public IReadOnlyList<string> Validate(AgentSmithConfig config)
    {
        var errors = new List<string>();
        ValidatePipelineTriggers(config.PipelineTriggers, errors);
        ValidateProjectTriggers(config.Projects, errors);
        ValidateProjectResolutionPresence(config.Projects, errors);
        ValidateRepoStrategySingleRepo(config.Projects, errors);
        ValidatePipelineOverrides(config.Projects, errors);
        return errors;
    }

    private static void ValidateProjectResolutionPresence(
        IReadOnlyDictionary<string, ResolvedProject> projects, List<string> errors)
    {
        foreach (var (name, project) in projects)
        {
            foreach (var (kind, trigger) in EnumerateTriggers(project))
            {
                if (trigger.ProjectResolution is not null) continue;
                errors.Add(
                    $"Project '{name}': {kind} is missing required 'project_resolution' " +
                    "(p0140 — declares how the webhook handler picks this project for an " +
                    "incoming ticket; one of tag/area-path/repo/to_address).");
            }
        }
    }

    private static void ValidateRepoStrategySingleRepo(
        IReadOnlyDictionary<string, ResolvedProject> projects, List<string> errors)
    {
        foreach (var (name, project) in projects)
        {
            foreach (var (kind, trigger) in EnumerateTriggers(project))
            {
                if (trigger.ProjectResolution is null) continue;
                if (trigger.ProjectResolution.Strategy != ResolutionStrategy.Repo) continue;
                if (project.Repos.Count == 1) continue;
                errors.Add(
                    $"Project '{name}': {kind} project_resolution.strategy=repo requires " +
                    $"exactly one entry in repos (has {project.Repos.Count}). Use strategy=tag " +
                    "or area-path for multi-repo projects, since the webhook payload's repo URL " +
                    "alone cannot disambiguate which repo of the project the ticket belongs to.");
            }
        }
    }

    private static void ValidatePipelineOverrides(
        IReadOnlyDictionary<string, ResolvedProject> projects, List<string> errors)
    {
        foreach (var (name, project) in projects)
        {
            foreach (var pipeline in project.Pipelines)
            {
                if (string.IsNullOrEmpty(pipeline.Name))
                {
                    errors.Add($"Project '{name}': pipelines entry has empty name.");
                    continue;
                }
                if (!PipelinePresets.Names.Contains(pipeline.Name))
                {
                    errors.Add(
                        $"Project '{name}': pipelines['{pipeline.Name}'] is not a known " +
                        $"pipeline (known: {string.Join(", ", PipelinePresets.Names)}).");
                }
                // AgentName resolution itself is handled by ResolvedProjectBuilder.
                // If the agent reference was unknown, the catalog resolver already failed and
                // we never reach this validator. So here we only re-affirm by checking that
                // when AgentName is set, Agent was successfully populated.
                if (!string.IsNullOrEmpty(pipeline.AgentName) && pipeline.Agent is null)
                {
                    errors.Add(
                        $"Project '{name}': pipelines['{pipeline.Name}'] declares agent " +
                        $"override '{pipeline.AgentName}' but the agent was not resolved " +
                        "from the catalog (should have been caught at load).");
                }
            }
        }
    }

    private static IEnumerable<(string Kind, WebhookTriggerConfig Trigger)> EnumerateTriggers(ResolvedProject project)
    {
        if (project.GithubTrigger is not null) yield return ("github_trigger", project.GithubTrigger);
        if (project.GitlabTrigger is not null) yield return ("gitlab_trigger", project.GitlabTrigger);
        if (project.AzuredevopsTrigger is not null) yield return ("azuredevops_trigger", project.AzuredevopsTrigger);
        if (project.JiraTrigger is not null) yield return ("jira_trigger", project.JiraTrigger);
    }

    private static void ValidatePipelineTriggers(PipelineTriggerMap map, List<string> errors)
    {
        foreach (var (label, pipelineName) in map.AsDictionary)
        {
            if (PipelinePresets.Names.Contains(pipelineName)) continue;
            errors.Add(
                $"pipeline_triggers['{label}'] references unknown pipeline " +
                $"'{pipelineName}' (known: {string.Join(", ", PipelinePresets.Names)}).");
        }
    }

    private static void ValidateProjectTriggers(
        IReadOnlyDictionary<string, ResolvedProject> projects, List<string> errors)
    {
        foreach (var (name, project) in projects)
        {
            ValidateTriggerForTrackerType(name, project, errors);
        }
    }

    private static void ValidateTriggerForTrackerType(
        string projectName, ResolvedProject project, List<string> errors)
    {
        var expected = ExpectedTriggerKind(project.Tracker.Type);
        var declared = DeclaredTriggers(project).ToList();
        if (declared.Count == 0) return;

        foreach (var actual in declared.Where(t => t != expected))
        {
            errors.Add(
                $"Project '{projectName}': has {actual} trigger but tracker " +
                $"'{project.Tracker.Name}' is type {project.Tracker.Type} " +
                $"(expected {expected} trigger).");
        }
    }

    private static string ExpectedTriggerKind(TrackerType type) => type switch
    {
        TrackerType.GitHub => "github_trigger",
        TrackerType.GitLab => "gitlab_trigger",
        TrackerType.AzureDevOps => "azuredevops_trigger",
        TrackerType.Jira => "jira_trigger",
        _ => "unknown",
    };

    private static IEnumerable<string> DeclaredTriggers(ResolvedProject project)
    {
        if (project.GithubTrigger is not null) yield return "github_trigger";
        if (project.GitlabTrigger is not null) yield return "gitlab_trigger";
        if (project.AzuredevopsTrigger is not null) yield return "azuredevops_trigger";
        if (project.JiraTrigger is not null) yield return "jira_trigger";
    }
}
