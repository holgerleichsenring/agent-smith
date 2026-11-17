using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// 2026-11-17-023fa: the project's merge block loads from YAML with underscored keys and enum values; a project
/// without the block is manual, which is how every project behaved before it existed.
/// </summary>
public sealed class MergePolicyYamlTests
{
    private const string Head = """
        agents:
          claude: { type: Claude, model: claude-sonnet-4-20250514 }
        secrets: { token: x }
        repos:
          app: { type: GitHub, url: https://github.com/example/app, auth: token }
        trackers:
          app: { type: GitHub, url: https://github.com/example/app, auth: token }
        projects:
          app:
            agent: claude
            tracker: app
            repos: [app]
            pipeline: code

        """;

    [Fact]
    public void MergeBlock_Loads_WhenAndProtection()
    {
        var project = Load(Head + "    merge:\n      when: on_gate_green\n      respect_branch_protection: true\n");

        project.Merge.When.Should().Be(MergeWhen.OnGateGreen);
        project.Merge.RespectBranchProtection.Should().BeTrue();
        project.Merge.Protection.Should().Be(BranchProtection.Respect);
    }

    // 2026-11-17-3435a
    [Fact]
    public void MergeBlock_Loads_CiEvidenceBounds()
    {
        var project = Load(Head + "    merge:\n      ci_excerpt_chars: 1500\n      ci_failed_checks_read: 2\n");

        project.Merge.CiExcerptChars.Should().Be(1500);
        project.Merge.CiFailedChecksRead.Should().Be(2);
        Load(Head).Merge.CiExcerptChars.Should().Be(4000, "the default when the block says nothing");
    }

    [Fact]
    public void NoMergeBlock_IsManual_AndWouldBypass()
    {
        var project = Load(Head);

        project.Merge.When.Should().Be(MergeWhen.Manual);
        project.Merge.Protection.Should().Be(BranchProtection.Bypass, "respect_branch_protection defaults to false");
    }

    private static ResolvedProject Load(string yaml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"merge-policy-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, yaml);
        try
        {
            var loader = new YamlConfigurationLoader(new RawConfigMaterializer(new ProjectConfigNormalizer(),
                new EffectiveTriggerBuilder(), new DeploymentDefaultsApplier(), new ConfigCatalogResolver(),
                new AgentSmithPaths()), new NoOpSystemEventPublisher());
            return loader.LoadConfig(path).Projects["app"];
        }
        finally
        {
            File.Delete(path);
        }
    }
}
