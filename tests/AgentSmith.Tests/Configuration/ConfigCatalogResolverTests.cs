using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// Covers p0139's catalog resolver + loader behavior: the four top-level
/// catalogs (agents/repos/trackers/pipeline_triggers) materialize into
/// records; project references resolve by name; unresolved references
/// fail fast with precise error messages.
/// </summary>
public sealed class ConfigCatalogResolverTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(),
        $"agentsmith-catalog-{Guid.NewGuid():N}.yml");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    [Fact]
    public void LoadConfig_ValidYml_PopulatesFourCatalogsAndResolvesProject()
    {
        Write("""
            agents:
              claude: { type: Claude, model: claude-sonnet-4-20250514 }
            repos:
              demo: { type: GitHub, url: https://github.com/x/y, auth: token }
            trackers:
              demo: { type: GitHub, url: https://github.com/x/y, auth: token }
            pipeline_triggers:
              bug: fix-bug
            projects:
              demo:
                agent: claude
                tracker: demo
                repos: [demo]
                pipeline: fix-bug
            secrets: {}
            """);

        var config = Load();

        config.Agents.Should().ContainKey("claude");
        config.Repos.Should().ContainKey("demo");
        config.Trackers.Should().ContainKey("demo");
        config.PipelineTriggers.AsDictionary.Should().ContainKey("bug");
        config.Projects.Should().ContainKey("demo");
        config.Projects["demo"].Agent.Type.Should().Be("Claude");
        config.Projects["demo"].Tracker.Type.Should().Be(TrackerType.GitHub);
        config.Projects["demo"].Repos.Single().Type.Should().Be(RepoType.GitHub);
    }

    [Fact]
    public void LoadConfig_ProjectReferencesUnknownAgent_ThrowsConfigurationException()
    {
        Write("""
            agents:
              real: { type: Claude }
            repos:
              r: { type: GitHub, auth: t }
            trackers:
              t: { type: GitHub, auth: t }
            projects:
              demo: { agent: typo, tracker: t, repos: [r] }
            """);

        var act = () => Load();
        act.Should().Throw<ConfigurationException>()
            .WithMessage("*references agent 'typo'*");
    }

    [Fact]
    public void LoadConfig_ProjectReferencesUnknownTracker_ThrowsConfigurationException()
    {
        Write("""
            agents:
              a: { type: Claude }
            repos:
              r: { type: GitHub, auth: t }
            trackers:
              real: { type: GitHub, auth: t }
            projects:
              demo: { agent: a, tracker: typo, repos: [r] }
            """);

        var act = () => Load();
        act.Should().Throw<ConfigurationException>()
            .WithMessage("*references tracker 'typo'*");
    }

    [Fact]
    public void LoadConfig_ProjectReferencesUnknownRepo_ThrowsConfigurationException()
    {
        Write("""
            agents:
              a: { type: Claude }
            repos:
              real: { type: GitHub, auth: t }
            trackers:
              t: { type: GitHub, auth: t }
            projects:
              demo: { agent: a, tracker: t, repos: [typo] }
            """);

        var act = () => Load();
        act.Should().Throw<ConfigurationException>()
            .WithMessage("*references repo 'typo'*");
    }

    [Fact]
    public void LoadConfig_MultipleErrors_AreReportedInOnePass()
    {
        Write("""
            agents:
              a: { type: Claude }
            repos:
              r: { type: GitHub, auth: t }
            trackers:
              t: { type: GitHub, auth: t }
            projects:
              one: { agent: typo-a, tracker: typo-t, repos: [typo-r] }
            """);

        var act = () => Load();
        var ex = act.Should().Throw<ConfigurationException>().Which;
        ex.Message.Should().Contain("agent 'typo-a'");
        ex.Message.Should().Contain("tracker 'typo-t'");
        ex.Message.Should().Contain("repo 'typo-r'");
    }

    [Fact]
    public void LoadConfig_RepoListWithOneEntry_ExposesItViaReposList()
    {
        Write("""
            agents:
              a: { type: Claude }
            repos:
              only: { type: GitHub, url: https://example.com, auth: t }
            trackers:
              t: { type: GitHub, auth: t }
            projects:
              demo: { agent: a, tracker: t, repos: [only] }
            """);

        var config = Load();

        config.Projects["demo"].Repos.Should().HaveCount(1);
        config.Projects["demo"].Repos.Single().Name.Should().Be("only");
        config.Projects["demo"].Repos.Single().Url.Should().Be("https://example.com");
    }

    // p0140a tests

    [Fact]
    public void LoadConfig_PipelineEntryWithAgentName_ResolvesAgentFromCatalog()
    {
        Write("""
            agents:
              fast: { type: Claude, model: claude-haiku-4-5-20251001 }
              big: { type: Claude, model: claude-opus-4-7 }
            repos:
              r: { type: GitHub, url: https://x, auth: t }
            trackers:
              t: { type: GitHub, auth: t }
            projects:
              demo:
                agent: fast
                tracker: t
                repos: [r]
                pipelines:
                  - { name: fix-bug, agent: big, skills_path: skills/heavy }
            """);

        var config = Load();

        var pipeline = config.Projects["demo"].Pipelines.Single(p => p.Name == "fix-bug");
        pipeline.AgentName.Should().Be("big");
        pipeline.Agent.Should().NotBeNull();
        pipeline.Agent!.Model.Should().Be("claude-opus-4-7");
        pipeline.SkillsPath.Should().Be("skills/heavy");
    }

    [Fact]
    public void LoadConfig_PipelineEntryReferencesUnknownAgent_ThrowsConfigurationException()
    {
        Write("""
            agents:
              real: { type: Claude }
            repos:
              r: { type: GitHub, url: https://x, auth: t }
            trackers:
              t: { type: GitHub, auth: t }
            projects:
              demo:
                agent: real
                tracker: t
                repos: [r]
                pipelines:
                  - { name: fix-bug, agent: ghost-agent }
            """);

        var act = () => Load();
        act.Should().Throw<ConfigurationException>()
            .WithMessage("*pipeline 'fix-bug' references agent 'ghost-agent'*");
    }

    private void Write(string yaml) => File.WriteAllText(_tempFile, yaml);

    private AgentSmithConfig Load() =>
        new YamlConfigurationLoader(
            new ProjectConfigNormalizer(),
            new ConfigCatalogResolver(),
            new AgentSmithPaths()).LoadConfig(_tempFile);
}
