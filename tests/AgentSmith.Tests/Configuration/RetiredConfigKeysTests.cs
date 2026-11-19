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

    // 2026-11-17-274cb: the six limits only the unused skill-call runtime read.
    [Fact]
    public void RetiredConfigKeys_SkillCallRuntimeLimits_EachReportsAdvisory()
    {
        const string yaml = """
            limits:
              max_tool_calls_per_skill: 30
              max_tool_calls_per_investigator: 10
              max_tool_calls_per_verifier: 20
              max_llm_calls_per_skill: 15
              max_input_tokens_per_skill_call: 200000
              max_concurrent_skill_calls: 10
              max_skills_per_phase: 5
            """;

        var fields = _detector.InYaml(yaml, "config/agentsmith.yml").Select(f => f.Field);

        fields.Should().BeEquivalentTo(
            "limits.max_tool_calls_per_investigator", "limits.max_tool_calls_per_verifier",
            "limits.max_llm_calls_per_skill", "limits.max_input_tokens_per_skill_call",
            "limits.max_concurrent_skill_calls", "limits.max_skills_per_phase");
    }

    // 2026-11-18-9c1ca: the switch of the post-turn review, which is gone.
    [Fact]
    public void RetiredConfigKeys_SpecReviewDesignTurn_ReportsAdvisory()
    {
        const string yaml = """
            spec_review:
              reground_read_budget: 24
              design_turn: true
            """;

        _detector.InYaml(yaml, "config/agentsmith.yml").Should().ContainSingle()
            .Which.Field.Should().Be("spec_review.design_turn");
    }

    // 2026-11-19-4176: the sizing of the run-time spec review, which is gone; the re-ground budget stays.
    [Fact]
    public void RetiredConfigKeys_SpecReviewSizing_ReportsOneAdvisoryPerKey()
    {
        const string yaml = """
            spec_review:
              rounds: 4
              reviewers: 2
              max_concurrent: 2
              budget_share_percent: 40
              attack_look_floor: 24
              series_contract_max_chars: 60000
              reground_read_budget: 24
            """;

        _detector.InYaml(yaml, "config/agentsmith.yml").Select(f => f.Field).Should().BeEquivalentTo(
            "spec_review.rounds", "spec_review.reviewers", "spec_review.max_concurrent", "spec_review.budget_share_percent",
            "spec_review.attack_look_floor", "spec_review.series_contract_max_chars");
    }

    [Fact]
    public void RetiredConfigKeys_ConfidenceThreshold_ReportsAdvisory()
    {
        const string yaml = """
            projects:
              todolist:
                pipelines:
                  - name: pr-review
                    confidence_threshold: 70
            """;

        var finding = _detector.InYaml(yaml, "config/agentsmith.yml").Should().ContainSingle().Which;

        finding.Severity.Should().Be(StartupFindingSeverity.Advisory);
        finding.Field.Should().Be("projects.todolist.pipelines.0.confidence_threshold");
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
