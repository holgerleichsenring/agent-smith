using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Credentials;

/// <summary>2026-10-02-5f89a: the small rules the migration and the catalog check share.</summary>
public sealed class LegacyCredentialRulesTests
{
    [Theory]
    [InlineData(RepoType.GitHub, "github_token", "GITHUB_TOKEN")]
    [InlineData(RepoType.GitLab, "gitlab_token", "GITLAB_TOKEN")]
    [InlineData(RepoType.AzureDevOps, "azure_devops_token", "AZURE_DEVOPS_TOKEN")]
    public void LegacyCredentialKeys_ForRepoType_IsTheExampleCatalogsName(RepoType type, string secret, string variable) =>
        LegacyCredentialKeys.For(type).Should().Be(new LegacyCredentialKey(secret, variable));

    [Fact]
    public void LegacyCredentialKeys_LocalRepo_HasNone() =>
        LegacyCredentialKeys.For(RepoType.Local).Should().BeNull();

    [Fact]
    public void LegacyCredentialKeys_JiraTracker_IsJiraToken() =>
        LegacyCredentialKeys.For(TrackerType.Jira).Should().Be(new LegacyCredentialKey("jira_token", "JIRA_TOKEN"));

    [Theory]
    [InlineData(RepoType.Local, "", true)]
    [InlineData(RepoType.Local, "NONE", true)]
    [InlineData(RepoType.Local, "a_secret", false)]
    [InlineData(RepoType.GitHub, "", false)]
    public void LocalRepoCredentials_IsExempt_OnlyForACredentialFreeLocalRepo(RepoType type, string auth, bool exempt) =>
        LocalRepoCredentials.IsExempt(new RawRepoEntry { Type = type, Auth = auth }).Should().Be(exempt);

    [Fact]
    public void MissingSecretFindings_Check_KnownSecretIsNoFinding() =>
        MissingSecretFindings.Check("repos", "Repo", "r", "GH", new HashSet<string>(["gh"], ConfigNames.Comparer))
            .Should().BeNull();

    [Fact]
    public void MissingSecretFindings_JiraWithoutEmail_IsBlocking() =>
        MissingSecretFindings.JiraWithoutEmail("j").IsBlocking.Should().BeTrue();

    [Fact]
    public void LegacyCredentialNotes_AreAdvisoryOnTheirOwnFields()
    {
        var key = new LegacyCredentialKey("gitlab_token", "GITLAB_TOKEN");
        StartupFinding[] notes =
        [
            LegacyCredentialNotes.Filled("repos.r", "Repo 'r'", key),
            LegacyCredentialNotes.Switched("repos.s", "Repo 's'", "other", key),
            LegacyCredentialNotes.EmptySecret("repos.r", "Repo 'r'", "gitlab_token"),
            LegacyCredentialNotes.CatalogAdded(key),
            LegacyCredentialNotes.FieldFilled("trackers.t", "Tracker 't'", "url", "JIRA_URL"),
        ];

        notes.Should().OnlyContain(n => n.Severity == StartupFindingSeverity.Advisory);
        notes.Select(n => n.Identity).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ConfigSecretReferences_Environment_ReadsTheNamedVariable() =>
        new ConfigSecretReferences(name => name == "X" ? "v" : null).Environment("X").Should().Be("v");
}
