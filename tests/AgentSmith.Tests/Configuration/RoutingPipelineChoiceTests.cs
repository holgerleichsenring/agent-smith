using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// 2026-09-25-3c7ad: the pipeline a label routes to is OUR closed set, and the form typed it
/// blind. The offer is derived from the property that makes two presets unroutable, so the next
/// such preset cannot quietly become offerable, and a name nobody offers is REPORTED rather than
/// blocking — a blocking finding disables every trigger on the project that carries it.
/// </summary>
public sealed class RoutingPipelineChoiceTests
{
    [Fact]
    public void Presets_TheOffer_ExcludesTheOnesATicketCannotRouteTo()
    {
        PipelinePresets.Routable.Should().NotContain(PipelinePresets.SpecDialogName,
            "a design conversation's run is seeded with a transcript and a reply slot");
        PipelinePresets.Routable.Should().NotContain("init-project",
            "project initialisation is launched with a context no label-routed run supplies");
    }

    [Fact]
    public void Presets_TheOffer_HoldsEveryOtherPreset()
    {
        PipelinePresets.Routable.Should().Contain(PipelinePresets.CodeName)
            .And.Contain("security-scan").And.Contain("pr-review");
        PipelinePresets.Routable.Should().HaveCount(PipelinePresets.Names.Count - 2,
            "the offer is DERIVED — it is the presets minus the two that need host-supplied "
            + "context, not a hand-written list that the next preset would silently join");
    }

    [Fact]
    public void Capabilities_TheRoutingValueAndTheDefault_CarryTheOffer()
    {
        var fields = TrackerCapabilityFields.For(TrackerType.Jira);

        fields.Single(f => f.Key == "pipelineFromLabel").Choices
            .Should().BeEquivalentTo(PipelinePresets.Routable);
        fields.Single(f => f.Key == "defaultPipeline").Choices
            .Should().BeEquivalentTo(PipelinePresets.Routable);
    }

    [Fact]
    public void Findings_AConfiguredPipelineNobodyOffers_IsReportedAndNotBlocking()
    {
        var findings = RoutingPipelineNames.Findings(Config("nonesuch")).ToList();

        findings.Should().ContainSingle()
            .Which.Severity.Should().Be(StartupFindingSeverity.Advisory,
                "blocking would disable every trigger on the project over one typo");
        findings[0].Reason.Should().Contain("nonesuch").And.Contain(PipelinePresets.CodeName);
    }

    [Fact]
    public void Findings_ARetiredAliasInAConfiguration_IsStillAccepted()
    {
        RoutingPipelineNames.Findings(Config("fix-bug")).Should().BeEmpty(
            "an alias is still a legal value until the harness and the callers move off it");
    }

    [Fact]
    public void Findings_AConfigurationNamingOnlyPresets_IsSilent()
    {
        RoutingPipelineNames.Findings(Config(PipelinePresets.CodeName)).Should().BeEmpty();
    }

    private static AgentSmithConfig Config(string pipeline) => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["alpha"] = new()
            {
                Name = "alpha",
                GithubTrigger = new WebhookTriggerConfig
                {
                    PipelineFromLabel = new Dictionary<string, string> { ["bug"] = pipeline },
                },
            },
        },
    };
}
