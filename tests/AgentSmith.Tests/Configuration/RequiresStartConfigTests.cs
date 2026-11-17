using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>2026-11-17-9a39a: <c>requires_start:</c> on a project — on_green_push by default, on_merge to opt out.</summary>
public sealed class RequiresStartConfigTests
{
    private const string Yaml = """
        agents:
          a: { provider: anthropic, model: m }
        trackers:
          t: { type: jira, url: https://t.test, project: P, auth: s, email: a@t.test }
        secrets: { s: x }
        repos:
          target: { url: https://git.test/target, auth: s }
        projects:
          app:
            agent: a
            tracker: t
            repos: [target]
            pipeline: code
        """;

    [Fact]
    public void LoadConfig_NoRequiresStart_StacksOnGreenPush()
    {
        var (config, findings) = Load(Yaml);

        findings.Should().BeEmpty();
        config.Projects["app"].RequiresStart.Should().Be(RequiresStart.OnGreenPush);
    }

    [Fact]
    public void LoadConfig_OnMerge_WaitsForTheMerge()
    {
        var (config, _) = Load(Yaml + "\n    requires_start: on_merge\n");

        config.Projects["app"].RequiresStart.Should().Be(RequiresStart.OnMerge);
    }

    [Fact]
    public void LoadConfig_UnknownValue_IsAFindingNamingIt()
    {
        var (config, findings) = Load(Yaml + "\n    requires_start: whenever\n");

        config.Projects.Should().NotContainKey("app");
        findings.Should().ContainSingle(f => f.Reason.Contains("requires_start 'whenever'") && f.Reason.Contains("on_merge"));
    }

    [Fact]
    public void Studio_ProjectionAndPatch_RoundTripTheSetting()
    {
        var stored = new RawProjectEntry { Agent = "a", Tracker = "t", RequiresStart = "on_merge" };

        ProjectEntityMapping.ToProject("app", stored).RequiresStart.Should().Be("on_merge");
        ProjectEntityMapping.ToProject("app", new RawProjectEntry()).RequiresStart.Should().Be("on_green_push");
        RawProjectPatch.Apply(new ProjectEntity("app", "a", "t", [], null, []), stored).RequiresStart.Should().Be("on_merge");
        RawProjectPatch.Apply(new ProjectEntity("app", "a", "t", [], null, [], RequiresStart: " "), stored).RequiresStart.Should().BeNull();
    }

    private static (AgentSmithConfig Config, IReadOnlyList<StartupFinding> Findings) Load(string yaml)
    {
        var resolver = new ConfigCatalogResolver();
        var config = resolver.Resolve(new RawConfigYaml().Deserialize(yaml));
        return (config, resolver.LastFindings);
    }
}
