using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// p0392: the editor asks the server what it would say, before the save. The rules are the
/// server's own — ClarificationParkStatusRule and ProjectTriggerRules through
/// ProjectConfigNormalizer.Inspect, and the descriptor's requiredness through
/// ConfigStudioCapabilities.ValidateTracker — so the studio can name a missing field
/// without holding an opinion about what a valid configuration is.
/// </summary>
public sealed class ConfigDraftRulesTests
{
    private readonly ConfigDraftRules _rules = new(new EffectiveTriggerBuilder(), new ProjectConfigNormalizer());

    [Fact]
    public void Studio_TriggerMissingNeedsClarificationStatus_IsFlaggedBeforeSave()
    {
        // The 2026-07-31 configuration, as the studio would submit it: a parking pipeline
        // and a tracker whose needs_clarification_status was never set. The server refused
        // to boot on this; the editor now refuses it first.
        var catalog = CatalogWith(Tracker(needsClarificationStatus: null));

        var findings = _rules.ForProject(Project(pipeline: "code"), catalog);

        findings.Should().ContainSingle(f =>
            f.Field == "needs_clarification_status" && f.IsBlocking && f.Project == "demo");
    }

    [Fact]
    public void Studio_TrackerSuppliesTheParkStatus_IsNotFlagged()
    {
        // The tracker OWNS the workflow (p0281b) and the trigger inherits it — so the check
        // has to run the real merge, not look at the project alone.
        var catalog = CatalogWith(Tracker(needsClarificationStatus: "question"));

        var findings = _rules.ForProject(Project(pipeline: "code"), catalog);

        findings.Should().NotContain(f => f.Field == "needs_clarification_status");
    }

    [Fact]
    public void Studio_NonParkingPipeline_IsNotFlagged()
    {
        // A scan-only project cannot park, so demanding the field would be noise.
        var catalog = CatalogWith(Tracker(needsClarificationStatus: null));

        var findings = _rules.ForProject(Project(pipeline: "security-scan"), catalog);

        findings.Should().NotContain(f => f.Field == "needs_clarification_status");
    }

    [Fact]
    public void Studio_RequiredFieldEmpty_BlocksSaveAndNamesTheField()
    {
        var draft = new TrackerEntity(Id: "gh", Type: "github", AuthSecret: null);

        var findings = _rules.ForTracker(draft);

        findings.Should().ContainSingle().Which.Reason
            .Should().Contain("url").And.Contain("authSecret");
    }

    [Fact]
    public void Studio_TrackerComplete_HasNoFindings()
    {
        var draft = new TrackerEntity(
            Id: "gh", Type: "github", AuthSecret: "GITHUB_TOKEN", Url: "https://github.com/x/y");

        _rules.ForTracker(draft).Should().BeEmpty();
    }

    private static ProjectEntity Project(string pipeline) => new(
        "demo", "claude", "gh", ["repo"], pipeline, [pipeline],
        new ProjectResolution("tag", "demo"));

    private static TrackerEntity Tracker(string? needsClarificationStatus) => new(
        Id: "gh",
        Type: "github",
        AuthSecret: "GITHUB_TOKEN",
        Url: "https://github.com/x/y",
        TriggerStatuses: ["open"],
        DoneStatus: "closed",
        NeedsClarificationStatus: needsClarificationStatus);

    private static ConfigCatalog CatalogWith(TrackerEntity tracker) =>
        new(Agents: [], Trackers: [tracker], Repos: [], Projects: [],
            McpServers: [], Secrets: [], Connections: []);
}
