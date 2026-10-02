using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Credentials;

/// <summary>
/// 2026-10-02-5f89a: a configuration that authenticated with the fixed variables keeps working —
/// its empty auths name the legacy secrets, the catalog gains them, and every fill is said.
/// </summary>
public sealed class LegacyCredentialMigrationTests
{
    private static LegacyCredentialMigration Migration(params (string Name, string Value)[] env)
    {
        var values = env.ToDictionary(e => e.Name, e => e.Value);
        return new LegacyCredentialMigration(
            new ConfigSecretReferences(name => values.TryGetValue(name, out var v) ? v : null));
    }

    [Fact]
    public void LegacyCredentialMigration_EmptyGitLabAuth_FillsGitlabTokenAndRecordsAnAdvisory()
    {
        var raw = new RawAgentSmithConfig();
        raw.Repos["api"] = new RawRepoEntry { Type = RepoType.GitLab, Url = "https://gitlab.example/g/api" };
        raw.Secrets["gitlab_token"] = "tok";

        var notes = Migration(("GITLAB_TOKEN", "tok")).Apply(raw);

        raw.Repos["api"].Auth.Should().Be("gitlab_token");
        notes.Should().ContainSingle(n => n.Field == "repos.api.auth")
            .Which.Severity.Should().Be(StartupFindingSeverity.Advisory);
    }

    [Fact]
    public void LegacyCredentialMigration_NoSecretsBlock_AddsTheLegacyCatalogEntry()
    {
        var raw = new RawAgentSmithConfig();
        raw.Connections["gh"] = new RawConnectionEntry { Type = RepoType.GitHub, Owner = "o" };

        var notes = Migration(("GITHUB_TOKEN", "ghp")).Apply(raw);

        raw.Connections["gh"].Auth.Should().Be("github_token");
        raw.Secrets.Should().Contain("github_token", "ghp");
        notes.Should().Contain(n => n.Field == "secrets.github_token");
        notes.Should().OnlyContain(n => n.Severity == StartupFindingSeverity.Advisory);
    }

    [Fact]
    public void LegacyCredentialMigration_LocalRepo_IsNeitherFilledNorChecked()
    {
        var raw = new RawAgentSmithConfig();
        raw.Repos["here"] = new RawRepoEntry { Type = RepoType.Local, Path = ".", Auth = "none" };
        raw.Repos["stub"] = new RawRepoEntry { Type = RepoType.Local, Path = ".", Url = "https://stub.test/x" };

        var notes = Migration(("GITHUB_TOKEN", "ghp")).Apply(raw);
        var findings = new List<StartupFinding>();
        new RepoCatalogBuilder().Build(raw.Repos, raw.Secrets.Keys, findings);

        raw.Repos["here"].Auth.Should().Be("none");
        raw.Repos["stub"].Auth.Should().BeEmpty();
        raw.Secrets.Should().BeEmpty();
        notes.Should().BeEmpty();
        findings.Should().BeEmpty();
    }

    [Fact]
    public void LegacyCredentialMigration_JiraWithoutEmail_TakesJiraEmailSecretThenEnvironment()
    {
        var fromSecret = Jira();
        fromSecret.Secrets["jira_email"] = "secret@example.com";
        var fromEnv = Jira();

        Migration(("JIRA_EMAIL", "env@example.com")).Apply(fromSecret);
        var notes = Migration(("JIRA_EMAIL", "env@example.com")).Apply(fromEnv);

        fromSecret.Trackers["j"].Email.Should().Be("secret@example.com");
        fromEnv.Trackers["j"].Email.Should().Be("env@example.com");
        notes.Should().Contain(n => n.Field == "trackers.j.email");
    }

    [Fact]
    public void LegacyCredentialMigration_GitlabUrlSet_FillsHostOfAReposEntryButNotOfAConnection()
    {
        var raw = new RawAgentSmithConfig();
        raw.Repos["api"] = new RawRepoEntry { Type = RepoType.GitLab, Url = "https://h.example/gitlab/g/api" };
        raw.Connections["gl"] = new RawConnectionEntry { Type = RepoType.GitLab, Group = "g" };

        Migration(("GITLAB_URL", "https://h.example/gitlab")).Apply(raw);

        raw.Repos["api"].Host.Should().Be("https://h.example/gitlab");
        raw.Connections["gl"].Host.Should().BeNull();
    }

    [Fact]
    public void LegacyCredentialMigration_JiraWithoutProject_KeepsTheDefaultKey()
    {
        var raw = Jira();

        var notes = Migration().Apply(raw);

        raw.Trackers["j"].Project.Should().BeNull("an unset project stays unset, so the transitioner keeps 'default'");
        notes.Should().NotContain(n => n.Field == "trackers.j.project");
    }

    [Fact]
    public void LegacyCredentialMigration_AuthNamesAnotherSecretWhileLegacyEnvSet_RecordsTheSwitch()
    {
        var raw = new RawAgentSmithConfig();
        raw.Trackers["ado"] = new RawTrackerEntry { Type = TrackerType.AzureDevOps, Auth = "team_pat" };
        raw.Secrets["team_pat"] = "new-value";

        var notes = Migration(("AZURE_DEVOPS_TOKEN", "old-value")).Apply(raw);

        notes.Should().ContainSingle(n => n.Field == "trackers.ado.auth")
            .Which.Reason.Should().Contain("'team_pat'").And.Contain("AZURE_DEVOPS_TOKEN")
            .And.NotContain("old-value").And.NotContain("new-value");
    }

    [Fact]
    public void LegacyCredentialMigration_NamedSecretEmpty_RecordsAnAdvisoryNotABlock()
    {
        var raw = new RawAgentSmithConfig();
        raw.Repos["r"] = new RawRepoEntry { Type = RepoType.GitHub, Url = "https://github.com/o/r", Auth = "gh" };
        raw.Secrets["gh"] = string.Empty;

        var notes = Migration().Apply(raw);

        notes.Should().ContainSingle(n => n.Field == "repos.r.auth-value")
            .Which.Severity.Should().Be(StartupFindingSeverity.Advisory);
    }

    private static RawAgentSmithConfig Jira()
    {
        var raw = new RawAgentSmithConfig();
        raw.Trackers["j"] = new RawTrackerEntry { Type = TrackerType.Jira, Url = "https://jira.example" };
        return raw;
    }
}
