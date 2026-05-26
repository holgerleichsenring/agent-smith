using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Triggers;

/// <summary>
/// p0140c: shared helper that returns the right WebhookTriggerConfig on a ResolvedProject
/// for a given match kind (github/gitlab/azuredevops/jira) OR tracker type. Used by both
/// the webhook spawn dispatcher (knows the match kind from ProjectMatch.Kind) and the
/// per-tracker pollers (know the tracker type).
/// </summary>
public static class TriggerSelectionHelper
{
    public static WebhookTriggerConfig? ByKind(ResolvedProject project, string kind) => kind switch
    {
        "github" => project.GithubTrigger,
        "gitlab" => project.GitlabTrigger,
        "azuredevops" => project.AzuredevopsTrigger,
        "jira" => project.JiraTrigger,
        _ => null,
    };

    public static WebhookTriggerConfig? ByTrackerType(ResolvedProject project, TrackerType type) => type switch
    {
        TrackerType.GitHub => project.GithubTrigger,
        TrackerType.GitLab => project.GitlabTrigger,
        TrackerType.AzureDevOps => project.AzuredevopsTrigger,
        TrackerType.Jira => project.JiraTrigger,
        _ => null,
    };
}
