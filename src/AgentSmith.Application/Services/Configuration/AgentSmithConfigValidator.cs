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
        return errors;
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
