using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-10-02-5f89a: the one place the fixed credential variables are still read. Before every
/// repo, connection and tracker authenticated with its own auth secret, a token was picked by
/// type from GITHUB_TOKEN, GITLAB_TOKEN, AZURE_DEVOPS_TOKEN or JIRA_TOKEN and auth was ignored.
/// An empty auth is filled with the type's legacy secret name, the catalog gains that entry when
/// it lacks it, and an auth that starts being read is named — so a configuration that worked
/// keeps working and the operator is told what changed. Every note is ADVISORY and only
/// recorded. Runs after secret resolution, before catalog resolution, on every load.
/// </summary>
public sealed class LegacyCredentialMigration(ConfigSecretReferences references)
{
    private readonly LegacyTrackerFieldMigration _trackerFields = new(references);

    public IReadOnlyList<StartupFinding> Apply(RawAgentSmithConfig raw)
    {
        var notes = new List<StartupFinding>();
        foreach (var (name, repo) in raw.Repos)
        {
            if (LocalRepoCredentials.IsExempt(repo)) continue;
            repo.Auth = Migrate(raw, $"repos.{name}", $"Repo '{name}'", repo.Auth,
                LegacyCredentialKeys.For(repo.Type), notes);
            FillGitLabHost(name, repo, notes);
        }
        foreach (var (name, connection) in raw.Connections)
            connection.Auth = Migrate(raw, $"connections.{name}", $"Connection '{name}'", connection.Auth,
                LegacyCredentialKeys.For(connection.Type), notes);
        foreach (var (name, tracker) in raw.Trackers)
        {
            tracker.Auth = Migrate(raw, $"trackers.{name}", $"Tracker '{name}'", tracker.Auth,
                LegacyCredentialKeys.For(tracker.Type), notes);
            _trackerFields.Apply(name, tracker, raw.Secrets, notes);
        }
        return notes;
    }

    private string Migrate(RawAgentSmithConfig raw, string field, string entity, string auth,
        LegacyCredentialKey? key, List<StartupFinding> notes)
    {
        if (key is not null)
        {
            auth = FillOrNoteSwitch(field, entity, auth, key, notes);
            if (ConfigNames.AreSame(auth, key.Secret)) AddCatalogEntry(raw, key, notes);
        }
        if (SecretValue(raw, auth) is { Length: 0 })
            notes.Add(LegacyCredentialNotes.EmptySecret(field, entity, auth));
        return auth;
    }

    private string FillOrNoteSwitch(string field, string entity, string auth, LegacyCredentialKey key,
        List<StartupFinding> notes)
    {
        if (string.IsNullOrWhiteSpace(auth))
        {
            notes.Add(LegacyCredentialNotes.Filled(field, entity, key));
            return key.Secret;
        }
        if (!ConfigNames.AreSame(auth, key.Secret)
            && !string.IsNullOrEmpty(references.Environment(key.Variable)))
            notes.Add(LegacyCredentialNotes.Switched(field, entity, auth, key));
        return auth;
    }

    private void AddCatalogEntry(RawAgentSmithConfig raw, LegacyCredentialKey key, List<StartupFinding> notes)
    {
        if (SecretValue(raw, key.Secret) is not null) return;
        raw.Secrets[key.Secret] = references.Environment(key.Variable) ?? string.Empty;
        notes.Add(LegacyCredentialNotes.CatalogAdded(key));
    }

    // GITLAB_URL overrode the base of every GitLab repo; a repo under repos: keeps that base as
    // its own host. A connection never read it, so its repos take the connection's host.
    private void FillGitLabHost(string name, RawRepoEntry repo, List<StartupFinding> notes)
    {
        if (repo.Type != RepoType.GitLab || !string.IsNullOrWhiteSpace(repo.Host)) return;
        var gitlabUrl = references.Environment("GITLAB_URL");
        if (string.IsNullOrWhiteSpace(gitlabUrl)) return;
        repo.Host = gitlabUrl;
        notes.Add(LegacyCredentialNotes.FieldFilled($"repos.{name}", $"Repo '{name}'", "host", "GITLAB_URL"));
    }

    private static string? SecretValue(RawAgentSmithConfig raw, string name) =>
        raw.Secrets.FirstOrDefault(s => ConfigNames.AreSame(s.Key, name)) is { Key: not null } hit
            ? hit.Value ?? string.Empty
            : null;
}
