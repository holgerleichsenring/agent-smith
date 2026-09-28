using System.Text.Json.Nodes;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration.Retired;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// The loaders ignore keys they do not bind, so a key this product stopped reading is silence
/// unless something reads the UNTYPED tree for it. One table, two sources — the file and the
/// stored documents — and an advisory finding that names the key and how to clear it.
/// </summary>
public sealed class RetiredConfigKeysTests
{
    private readonly RetiredConfigKeyDetector _detector = new(new RawConfigTreeReader(), new ConfigKeyPathMatcher());

    [Fact]
    public void RetiredConfigKeys_ParentLinkType_ReportsAdvisory()
    {
        const string yaml = """
            trackers:
              jira-main:
                type: jira
                parent_link_type: Relates
            """;

        var finding = _detector.InYaml(yaml, "config/agentsmith.yml").Should().ContainSingle().Which;

        finding.Severity.Should().Be(StartupFindingSeverity.Advisory);
        finding.Subsystem.Should().Be(StartupSubsystems.Configuration);
        finding.Field.Should().Be("trackers.jira-main.parent_link_type");
        finding.Reason.Should().Contain("config/agentsmith.yml").And.Contain("Delete it from the file");
    }

    [Fact]
    public void RetiredConfigKeys_ParentLinkTypeInAStoredTracker_ReportsAdvisory()
    {
        ConfigDocRow[] rows =
        [
            new("tracker", "jira-main", """{"Type":"jira","ParentLinkType":"Relates"}""", 3),
            new("tracker", "github", """{"Type":"github","ParentLinkType":null}""", 1),
        ];

        var finding = _detector.InStoredDocuments(rows).Should().ContainSingle(
            "a stored document serialises every property, so a null one is unset, not retired").Which;

        finding.Field.Should().Be("trackers.jira-main.parent_link_type");
        finding.Reason.Should().Contain("stored configuration").And.Contain("studio");
    }

    [Fact]
    public void RetiredConfigKeys_AFileWithoutRetiredKeys_IsSilent()
    {
        _detector.InYaml("trackers:\n  t:\n    type: jira\n    parent_link_type: ~\n", "c.yml")
            .Should().BeEmpty();
        _detector.InYaml(string.Empty, "c.yml").Should().BeEmpty();
    }

    [Fact]
    public void RetiredConfigKeys_EveryRow_IsADottedPathWithASinceAndAReason()
    {
        RetiredConfigKeys.All.Should().OnlyContain(k =>
            !string.IsNullOrWhiteSpace(k.Path) && !k.Path.Contains(' ')
            && !string.IsNullOrWhiteSpace(k.Since) && !string.IsNullOrWhiteSpace(k.Reason));
    }

    [Fact]
    public void Matcher_WildcardOverAList_NamesTheElementIndex()
    {
        var tree = JsonNode.Parse("""{"projects":{"p":{"pipelines":[{"name":"a"},{"name":"b","x":1}]}}}""");

        new ConfigKeyPathMatcher().SetPaths(tree, "projects.*.pipelines.*.x")
            .Should().Equal("projects.p.pipelines.1.x");
    }

    [Fact]
    public void Matcher_ABlankOrMissingValue_IsNotSet()
    {
        var tree = JsonNode.Parse("""{"a":{"b":"  "},"c":{}}""");

        new ConfigKeyPathMatcher().SetPaths(tree, "*.b").Should().BeEmpty();
    }

    [Fact]
    public void TreeReader_StoredSingletonAndCollection_SitUnderTheirYamlRoots()
    {
        var tree = new RawConfigTreeReader().FromStoredDocuments(
        [
            new("orchestrator", "default", """{"Enabled":true}""", 1),
            new("mcp_server", "docs", """{"Url":"u"}""", 1),
        ]);

        tree["orchestrator"]!["Enabled"]!.GetValue<bool>().Should().BeTrue();
        tree["mcp_servers"]!["docs"]!["Url"]!.GetValue<string>().Should().Be("u");
    }

    [Fact]
    public void TreeReader_Yaml_KeepsKeysNoModelBinds()
    {
        var tree = new RawConfigTreeReader().FromYaml("root:\n  gone: 1\n  list: [a, b]\n");

        tree!["root"]!["gone"]!.GetValue<string>().Should().Be("1");
        tree["root"]!["list"]!.AsArray().Should().HaveCount(2);
    }

    [Fact]
    public void TrackerCapabilityFields_NoParentLinkType()
    {
        foreach (var type in Enum.GetValues<TrackerType>())
            TrackerCapabilityFields.For(type).Should().NotContain(f => f.Key == "parentLinkType");
    }
}
