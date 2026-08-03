using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// p0391b: ConfigCatalogResolver used to aggregate every configuration error into one
/// throw, which the server's loader then turned into a single "the stored configuration is
/// not usable" line — six mistakes, one sentence, and every healthy project silenced with
/// them. Each error is now its own finding, naming the project and the field to edit, and
/// only the project that carries it drops out.
/// </summary>
public sealed class ConfigCatalogResolverFindingsTests
{
    [Fact]
    public void ConfigCatalogResolver_SixErrors_ProducesSixFindingsWithTheirFields()
    {
        var raw = RawConfigYaml.Deserialize("""
            agents:
              real: { type: Claude }
            repos:
              real: { type: GitHub, auth: t }
            trackers:
              real: { type: GitHub, auth: t }
            projects:
              one: { agent: typo-a, tracker: typo-t, repos: [typo-r] }
              two: { agent: typo-a2, tracker: typo-t2, repos: [typo-r2] }
            """);

        var findings = Resolve(raw).Findings;

        findings.Should().HaveCount(6);
        findings.Should().OnlyContain(f => f.Subsystem == StartupSubsystems.Configuration);
        findings.Should().OnlyContain(f => f.IsBlocking);
        findings.Select(f => (f.Project, f.Field)).Should().BeEquivalentTo(
        [
            ("one", "agent"), ("one", "tracker"), ("one", "repos"),
            ("two", "agent"), ("two", "tracker"), ("two", "repos"),
        ]);
    }

    [Fact]
    public void ConfigCatalogResolver_NoErrors_ProducesNone()
    {
        var raw = RawConfigYaml.Deserialize("""
            agents:
              a: { type: Claude }
            repos:
              r: { type: GitHub, url: https://x, auth: t }
            trackers:
              t: { type: GitHub, auth: t }
            projects:
              demo: { agent: a, tracker: t, repos: [r] }
            """);

        var resolved = Resolve(raw);

        resolved.Findings.Should().BeEmpty();
        resolved.Config.Projects.Should().ContainKey("demo");
    }

    [Fact]
    public void ConfigCatalogResolver_OneBrokenProject_KeepsTheOthers()
    {
        // The point of the conversion: an unresolvable reference disables ITS project, not
        // the configuration. Before p0391b this threw and the server ran with nothing.
        var raw = RawConfigYaml.Deserialize("""
            agents:
              a: { type: Claude }
            repos:
              r: { type: GitHub, url: https://x, auth: t }
            trackers:
              t: { type: GitHub, auth: t }
            projects:
              healthy: { agent: a, tracker: t, repos: [r] }
              broken: { agent: ghost, tracker: t, repos: [r] }
            """);

        var resolved = Resolve(raw);

        resolved.Config.Projects.Should().ContainKey("healthy");
        resolved.Config.Projects.Should().NotContainKey("broken");
        resolved.Findings.Should().ContainSingle().Which.Project.Should().Be("broken");
    }

    [Fact]
    public void ConfigCatalogResolver_UnknownResolutionStrategy_IsThatProjectsFinding()
    {
        // Used to throw out of EffectiveTriggerBuilder before the resolver ran at all, so
        // the whole configuration came back empty for one typo in one project.
        var raw = RawConfigYaml.Deserialize("""
            agents:
              a: { type: Claude }
            repos:
              r: { type: GitHub, url: https://x, auth: t }
            trackers:
              t: { type: GitHub, auth: t }
            projects:
              healthy:
                agent: a
                tracker: t
                repos: [r]
                resolution: { tag: ok }
              broken:
                agent: a
                tracker: t
                repos: [r]
                resolution: { not_a_strategy: whatever }
            """);

        var resolved = Resolve(raw);

        resolved.Config.Projects.Should().ContainKey("healthy");
        resolved.Findings.Should().ContainSingle(f =>
            f.Project == "broken" && f.Field == "resolution" && f.IsBlocking);
    }

    private static (AgentSmithConfig Config, IReadOnlyList<StartupFinding> Findings) Resolve(
        RawAgentSmithConfig raw)
    {
        var materializer = new RawConfigMaterializer(
            new ProjectConfigNormalizer(),
            new EffectiveTriggerBuilder(),
            new DeploymentDefaultsApplier(),
            new ConfigCatalogResolver(),
            new AgentSmithPaths());
        var config = materializer.Materialize(raw);
        return (config, materializer.LastResolutionFindings);
    }
}
