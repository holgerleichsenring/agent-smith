using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Retired;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Tests.Architecture;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// An import names every key it does not store. It used to drop them in silence, so a
/// misspelt key looked imported and did nothing.
/// </summary>
public sealed class ConfigImportPlannerTests
{
    private static readonly ConfigImportPlanner Planner = new(
        new RawConfigYaml(), new ConfigDocumentAssembler(), new RawConfigTreeReader(), new StoredKeyDiff(),
        new RetiredConfigKeyDetector(new RawConfigTreeReader(), new ConfigKeyPathMatcher()));

    [Fact]
    public void Plan_MisspeltKey_IsNamed()
    {
        var plan = Planner.Plan("""
            agents:
              a:
                type: openai
                modle: gpt-5
            """, "test.yml");

        plan.Dropped.Should().ContainSingle(d => d.Path == "agents.a.modle")
            .Which.Reason.Should().Contain("no setting has this name");
    }

    [Fact]
    public void Plan_RetiredKey_IsNamedWithItsReason()
    {
        var plan = Planner.Plan("""
            agents:
              a:
                type: claude
                compaction:
                  summary_model: claude-haiku-4-5-20251001
            """, "test.yml");

        plan.Dropped.Should().ContainSingle(d => d.Path == "agents.a.compaction.summary_model")
            .Which.Reason.Should().Contain("summarization role");
    }

    [Fact]
    public void Plan_BootstrapBlock_IsNamedAsFileOnly()
    {
        var plan = Planner.Plan("tool_runner:\n  type: auto\n", "test.yml");

        plan.Dropped.Should().ContainSingle(d => d.Path == "tool_runner")
            .Which.Reason.Should().Contain("never stored");
        plan.Docs.Should().NotContain(d => d.Type == ConfigDocTypes.Persistence);
    }

    [Fact]
    public void Plan_ShippedExample_DropsOnlyTheBootstrapBlocks()
    {
        var plan = Planner.Plan(File.ReadAllText(ConfigSchemaFile.ExamplePath), "example");

        plan.Dropped.Select(d => d.Path).Should().BeEquivalentTo(["persistence", "tool_runner"],
            "everything else the example sets is a setting the store keeps");
    }

    [Fact]
    public void Plan_RepoMappingForm_IsKept()
    {
        var plan = Planner.Plan("""
            projects:
              p:
                agent: a
                tracker: t
                repos:
                  - repo: c/App
                    default_branch: develop
            """, "test.yml");

        plan.Dropped.Should().BeEmpty();
    }
}
