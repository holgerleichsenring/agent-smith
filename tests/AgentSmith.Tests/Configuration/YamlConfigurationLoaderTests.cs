using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

public class YamlConfigurationLoaderTests
{
    private readonly YamlConfigurationLoader _loader =
        new(new ProjectConfigNormalizer(), new ConfigCatalogResolver(), new AgentSmithPaths());

    private static string TestDataPath(string fileName)
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Configuration", "TestData", fileName);
    }

    [Fact]
    public void LoadConfig_ValidYaml_ReturnsConfig()
    {
        var config = _loader.LoadConfig(TestDataPath("valid-config.yml"));

        config.Should().NotBeNull();
        config.Projects.Should().ContainKey("testproject");
    }

    [Fact]
    public void LoadConfig_FileNotFound_ThrowsConfigurationException()
    {
        var act = () => _loader.LoadConfig("nonexistent.yml");

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void LoadConfig_InvalidYaml_ThrowsConfigurationException()
    {
        var act = () => _loader.LoadConfig(TestDataPath("invalid-config.yml"));

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*Invalid YAML*");
    }

    [Fact]
    public void LoadConfig_WithEnvVars_ResolvesPlaceholders()
    {
        Environment.SetEnvironmentVariable("AGENTSMITH_TEST_GH_TOKEN", "test-token-123");

        var config = _loader.LoadConfig(TestDataPath("valid-config.yml"));

        config.Secrets["github_token"].Should().Be("test-token-123");

        Environment.SetEnvironmentVariable("AGENTSMITH_TEST_GH_TOKEN", null);
    }

    [Fact]
    public void LoadConfig_ProjectHasAllFields_MapsCorrectly()
    {
        var config = _loader.LoadConfig(TestDataPath("valid-config.yml"));
        var project = config.Projects["testproject"];

        var repo = project.Repos.Single();
        repo.Type.Should().Be(RepoType.GitHub);
        repo.Url.Should().Be("https://github.com/test/repo");
        project.Tracker.Type.Should().Be(TrackerType.AzureDevOps);
        project.Tracker.Organization.Should().Be("testorg");
        project.Agent.Type.Should().Be("claude");
        project.Agent.Model.Should().Be("sonnet-4");
        project.Pipeline.Should().Be("fix-bug");
    }

    // Loads the bundled operator-facing example end-to-end. Regression guard for
    // the p0140a area-path bug: a `strategy: area-path` value in the example
    // never matched ResolutionStrategy.AreaPath because YamlDotNet's underscored
    // property convention does not apply to enum values, and no end-to-end load
    // covered the canonical example. This test exercises the full deserializer
    // wiring (snake_case enums) against the live example file.
    [Fact]
    public void LoadConfig_BundledExample_LoadsAndBindsEnumsAcrossAllCanonicalForms()
    {
        var config = _loader.LoadConfig(TestDataPath("agentsmith.example.yml"));

        config.Should().NotBeNull();
        config.Repos.Values.Select(r => r.Type).Should().Contain(
            new[] { RepoType.GitHub, RepoType.GitLab, RepoType.AzureDevOps, RepoType.Local });
        config.Trackers.Values.Select(t => t.Type).Should().Contain(
            new[] { TrackerType.GitHub, TrackerType.AzureDevOps, TrackerType.Jira });

        var strategies = config.Projects.Values
            .SelectMany(p => new[] { p.GithubTrigger, p.GitlabTrigger, p.AzuredevopsTrigger, p.JiraTrigger })
            .Where(t => t?.ProjectResolution is not null)
            .Select(t => t!.ProjectResolution!.Strategy)
            .ToList();
        strategies.Should().Contain(ResolutionStrategy.AreaPath,
            "the example exercises ADO area_path resolution — this is the assertion that fails when YamlDotNet's enum naming convention isn't wired up");
        strategies.Should().Contain(ResolutionStrategy.Tag);
        strategies.Should().Contain(ResolutionStrategy.Repo);
    }
}
