using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
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

        // 2026-09-16-a4d7 added a second, ADVISORY finding to the same draft (it declares
        // no routing either), so the blocking one is selected rather than assumed alone.
        findings.Should().ContainSingle(f => f.IsBlocking).Which.Reason
            .Should().Contain("url").And.Contain("authSecret");
    }

    [Fact]
    public void Tracker_NoMapAndNoDefault_ReportsWhereTicketsGo()
    {
        // Neither a label map nor a fallback: every ticket this tracker routes runs the
        // hardcoded preset, and until now nothing said so anywhere.
        var draft = new TrackerEntity(
            Id: "gh", Type: "github", AuthSecret: "GITHUB_TOKEN", Url: "https://github.com/x/y");

        var findings = _rules.ForTracker(draft);

        var advisory = findings.Should().ContainSingle().Which;
        advisory.IsBlocking.Should().BeFalse();
        advisory.Field.Should().Be("defaultPipeline");
        advisory.Reason.Should().Contain(PipelinePresets.UndeclaredFallbackPipeline);
    }

    [Fact]
    public void Tracker_ExistingTrackerWithoutIt_StillSaves()
    {
        // The descriptor declares the field OPTIONAL on purpose: Required is enforced by
        // ValidateTracker on every upsert and surfaced as a BLOCKING draft finding, so
        // requiring it would make every tracker configured before this phase unsaveable.
        var draft = new TrackerEntity(
            Id: "gh", Type: "github", AuthSecret: "GITHUB_TOKEN", Url: "https://github.com/x/y");

        _rules.ForTracker(draft).Should().NotContain(f => f.IsBlocking);
        FluentActions.Invoking(() => ConfigStudioCapabilities.ValidateTracker(draft)).Should().NotThrow();
    }

    [Fact]
    public void Studio_TrackerComplete_HasNoFindings()
    {
        // 2026-09-16-a4d7: "complete" now includes declaring where an unrouted ticket goes;
        // a tracker that declares neither a map nor a default carries the advisory finding.
        var draft = new TrackerEntity(
            Id: "gh", Type: "github", AuthSecret: "GITHUB_TOKEN", Url: "https://github.com/x/y",
            DefaultPipeline: "code");

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
