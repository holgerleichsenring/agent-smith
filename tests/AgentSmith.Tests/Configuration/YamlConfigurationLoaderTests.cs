using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

public class YamlConfigurationLoaderTests
{
    private readonly YamlConfigurationLoader _loader =
        new(new ProjectConfigNormalizer(), new ConfigCatalogResolver(), new AgentSmithPaths(), new NoOpSystemEventPublisher());

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
    public void LoadConfig_RegistriesBlock_RoundTripsWithSecretSubstitution()
    {
        Environment.SetEnvironmentVariable("AGENTSMITH_TEST_AZURE_ARTIFACTS_TOKEN", "azure-token-xyz");
        Environment.SetEnvironmentVariable("AGENTSMITH_TEST_JFROG_TOKEN", "jfrog-token-abc");
        try
        {
            var config = _loader.LoadConfig(TestDataPath("registries-config.yml"));

            config.Registries.Should().HaveCount(2);
            config.Registries[0].Host.Should().Be("pkgs.dev.azure.com");
            config.Registries[0].Username.Should().Be("any");
            config.Registries[0].Token.Should().Be("azure-token-xyz");
            config.Registries[1].Host.Should().Be("my-company.jfrog.io");
            config.Registries[1].Token.Should().Be("jfrog-token-abc");
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENTSMITH_TEST_AZURE_ARTIFACTS_TOKEN", null);
            Environment.SetEnvironmentVariable("AGENTSMITH_TEST_JFROG_TOKEN", null);
        }
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

    // Regression: pre-fix the top-level pipeline_cost_cap YAML block was
    // silently dropped because RawAgentSmithConfig had no PipelineCostCap
    // slot (YamlDotNet's IgnoreUnmatchedProperties() hid the binding gap),
    // and ConfigCatalogResolver.Compose did not propagate it. Effect:
    // PipelineCostCapConfig.ResolveFor always returned the hardcoded
    // $5 / 500k default — the per_pipeline override never took effect,
    // capping api-security-scan runs early (observed 2026-05-27: 890k
    // tokens / $1.81 capped despite a per_pipeline override of 2M tokens).
    [Fact]
    public void LoadConfig_PipelineCostCap_PerPipelineOverrideReachesResolver()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"agentsmith-costcap-{Guid.NewGuid():N}.yml");
        File.WriteAllText(tempFile, """
            agents:
              claude-default:
                type: claude
                model: sonnet-4

            repos:
              test-repo:
                type: github
                url: https://github.com/test/repo
                auth: token

            trackers:
              test-ado:
                type: azure_devops
                organization: testorg
                project: TestProject
                auth: token

            projects:
              testproject:
                agent: claude-default
                tracker: test-ado
                repos: [test-repo]
                pipeline: fix-bug

            pipeline_cost_cap:
              default:
                usd: 3
                tokens: 250000
              per_pipeline:
                api-security-scan:
                  usd: 5
                  tokens: 2000000
            """);
        try
        {
            var config = _loader.LoadConfig(tempFile);

            config.PipelineCostCap.Default.Usd.Should().Be(3m,
                "the top-level pipeline_cost_cap.default YAML block must bind to AgentSmithConfig.PipelineCostCap.Default");
            config.PipelineCostCap.Default.Tokens.Should().Be(250_000);

            // p0270a: resolution lives in ConfigResolutionPass now; this loader
            // test asserts the per_pipeline override BOUND from YAML (Raw → Compose
            // → AgentSmithConfig). "fix-bug" has no entry, so it resolves to Default.
            var perPipeline = config.PipelineCostCap.PerPipeline["api-security-scan"];
            perPipeline.Usd.Should().Be(5m,
                "per_pipeline.api-security-scan override must bind, not fall back to the default");
            perPipeline.Tokens.Should().Be(2_000_000,
                "per_pipeline.api-security-scan.tokens override must propagate through Raw → Compose → AgentSmithConfig");

            config.PipelineCostCap.PerPipeline.ContainsKey("fix-bug").Should().BeFalse(
                "pipelines without a per_pipeline entry resolve to pipeline_cost_cap.default");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
