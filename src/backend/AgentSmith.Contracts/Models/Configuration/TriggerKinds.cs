namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0391a: the one vocabulary for naming a project's trigger block. A finding names the
/// trigger it disables and discovery/webhook dispatch look it up by the same name, so the
/// two must not each spell it their own way — the tracker type and the webhook match kind
/// both map here rather than into ad-hoc strings.
/// </summary>
public static class TriggerKinds
{
    public const string Jira = "jira_trigger";
    public const string GitHub = "github_trigger";
    public const string GitLab = "gitlab_trigger";
    public const string AzureDevOps = "azuredevops_trigger";
    public const string Unknown = "unknown";

    public static string ForTracker(TrackerType type) => type switch
    {
        TrackerType.GitHub => GitHub,
        TrackerType.GitLab => GitLab,
        TrackerType.AzureDevOps => AzureDevOps,
        TrackerType.Jira => Jira,
        _ => Unknown,
    };

    /// <summary>Maps a webhook <c>ProjectMatch.Kind</c> ("github", "jira", …) to the block name.</summary>
    public static string ForMatchKind(string kind) => kind switch
    {
        "github" => GitHub,
        "gitlab" => GitLab,
        "azuredevops" => AzureDevOps,
        "jira" => Jira,
        _ => Unknown,
    };
}
