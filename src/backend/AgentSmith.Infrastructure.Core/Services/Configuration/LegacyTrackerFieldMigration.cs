using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-10-02-5f89a: the tracker fields the factories used to take from the environment when
/// the tracker left them empty — the GitLab base url and project, the Jira url, project and
/// email — written into the tracker once at load, each fill recorded, so the factories read the
/// tracker alone. A Jira project still empty keeps the transitioner's 'default' key.
/// </summary>
public sealed class LegacyTrackerFieldMigration(ConfigSecretReferences references)
{
    private const string JiraEmailSecret = "jira_email";

    public void Apply(string name, RawTrackerEntry tracker, IReadOnlyDictionary<string, string> secrets,
        List<StartupFinding> notes)
    {
        var field = $"trackers.{name}";
        var entity = $"Tracker '{name}'";
        if (tracker.Type == TrackerType.GitLab)
        {
            tracker.Url = Fill(field, entity, "url", tracker.Url, "GITLAB_URL", notes);
            tracker.Project = Fill(field, entity, "project", tracker.Project, "GITLAB_PROJECT", notes);
        }
        if (tracker.Type != TrackerType.Jira) return;
        tracker.Url = Fill(field, entity, "url", tracker.Url, "JIRA_URL", notes);
        tracker.Project = Fill(field, entity, "project", tracker.Project, "JIRA_PROJECT", notes);
        tracker.Email = FillEmail(field, entity, tracker.Email, secrets, notes);
    }

    private string? Fill(string field, string entity, string key, string? current, string variable,
        List<StartupFinding> notes)
    {
        if (!string.IsNullOrWhiteSpace(current)) return current;
        var value = references.Environment(variable);
        if (string.IsNullOrWhiteSpace(value)) return current;
        notes.Add(LegacyCredentialNotes.FieldFilled(field, entity, key, variable));
        return value;
    }

    private string? FillEmail(string field, string entity, string? current,
        IReadOnlyDictionary<string, string> secrets, List<StartupFinding> notes)
    {
        if (!string.IsNullOrWhiteSpace(current)) return current;
        var fromSecret = secrets.FirstOrDefault(s => ConfigNames.AreSame(s.Key, JiraEmailSecret)).Value;
        if (string.IsNullOrWhiteSpace(fromSecret))
            return Fill(field, entity, "email", current, "JIRA_EMAIL", notes);
        notes.Add(LegacyCredentialNotes.FieldFilled(field, entity, "email", $"secret '{JiraEmailSecret}'"));
        return fromSecret;
    }
}
