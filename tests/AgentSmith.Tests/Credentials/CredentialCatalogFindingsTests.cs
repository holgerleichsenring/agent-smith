using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Credentials;

/// <summary>
/// 2026-10-02-5f89a: an auth naming nothing is BLOCKING and the entry stays; a migration note
/// is ADVISORY and never stops the one-shot loader.
/// </summary>
public sealed class CredentialCatalogFindingsTests
{
    [Fact]
    public void ConnectionCatalogBuilder_AuthNamingNoSecret_IsBlockingAndTheEntryStays()
    {
        var raw = new Dictionary<string, RawConnectionEntry>
        {
            ["gl"] = new() { Type = RepoType.GitLab, Group = "g", Auth = "gitlab_typo" },
        };
        var findings = new List<StartupFinding>();

        var built = new ConnectionCatalogBuilder().Build(raw, ["gitlab_token"], findings);

        built.Should().ContainKey("gl");
        findings.Should().ContainSingle().Which.Should().Match<StartupFinding>(f =>
            f.IsBlocking && f.Reason.Contains("'gitlab_typo'") && f.Field == "connections:gl");
    }

    [Fact]
    public void TrackerCatalogBuilder_JiraWithoutEmail_IsBlocking()
    {
        var raw = new Dictionary<string, RawTrackerEntry>
        {
            ["j"] = new() { Type = TrackerType.Jira, Url = "https://jira.example", Auth = "jira_token" },
        };
        var findings = new List<StartupFinding>();

        new TrackerCatalogBuilder().Build(raw, ["jira_token"], findings);

        findings.Should().ContainSingle(f => f.IsBlocking && f.Field == "trackers:j.email");
    }

    [Fact]
    public void YamlConfigurationLoader_ConfigWithOnlyMigrationAdvisories_Loads()
    {
        var path = Path.Combine(Path.GetTempPath(), $"legacy-creds-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, """
            repos:
              api:
                type: gitlab
                url: https://gitlab.example/g/api
            trackers:
              board:
                type: jira
                url: https://jira.example
            """);
        var env = new Dictionary<string, string> { ["GITLAB_TOKEN"] = "gl", ["JIRA_TOKEN"] = "jt", ["JIRA_EMAIL"] = "a@b.example" };
        var findings = new StartupFindings();
        var loader = new YamlConfigurationLoader(new RawConfigMaterializer(
            new ProjectConfigNormalizer(), new EffectiveTriggerBuilder(), new DeploymentDefaultsApplier(),
            new ConfigCatalogResolver(findings: findings), new AgentSmithPaths(), findings,
            new ConfigSecretReferences(name => env.GetValueOrDefault(name))), new NoOpSystemEventPublisher());

        try
        {
            var config = loader.LoadConfig(path);

            config.Repos["api"].Auth.Should().Be("gitlab_token");
            config.Trackers["board"].Email.Should().Be("a@b.example");
            config.Secrets.Should().Contain("gitlab_token", "gl").And.Contain("jira_token", "jt");
            findings.All.Should().NotBeEmpty().And.OnlyContain(f => !f.IsBlocking);
        }
        finally { File.Delete(path); }
    }
}
