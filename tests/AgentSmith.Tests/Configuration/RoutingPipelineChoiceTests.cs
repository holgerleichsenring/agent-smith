using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// The pipeline a label routes to is OUR closed set. The offer is derived from the property that
/// makes a preset unroutable — a run launched with context only a host supplies — so the next
/// such preset cannot quietly become offerable, and a name nobody offers is REPORTED rather than
/// blocking: a blocking finding disables every trigger on the project that carries it.
/// </summary>
public sealed class RoutingPipelineChoiceTests
{
    [Fact]
    public void Presets_TheOffer_ExcludesTheOneATicketCannotRouteTo()
    {
        PipelinePresets.Routable.Should().NotContain(PipelinePresets.SpecDialogName,
            "a design conversation's run is seeded with a transcript and a reply slot");
        PipelinePresets.NeedsHostContext(PipelinePresets.SpecDialogName).Should().BeTrue();
    }

    [Fact]
    public void Routable_ContainsInitProject()
    {
        PipelinePresets.Routable.Should().Contain("init-project",
            "a label starts project initialisation on the ticket's branch like any other preset");
        PipelinePresets.NeedsHostContext("init-project").Should().BeFalse();
    }

    [Fact]
    public void Presets_TheOffer_HoldsEveryOtherPreset()
    {
        PipelinePresets.Routable.Should().Contain(PipelinePresets.CodeName)
            .And.Contain("security-scan").And.Contain("pr-review");
        PipelinePresets.Routable.Should().HaveCount(PipelinePresets.Names.Count - 1,
            "the offer is DERIVED — it is the presets minus the one that needs host-supplied "
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
    public void Findings_ARetiredNameInAConfiguration_IsReportedAndNamesItsReplacement()
    {
        // 2026-09-25-e5b1: while the alias map existed this was a legal value and this check
        // stayed silent. It is now the check that catches an operator who never rewrote it —
        // and "offered: code, security-scan, …" would leave them guessing which of those their
        // old word became, so the finding says the one word that replaced it.
        var findings = RoutingPipelineNames.Findings(Config("fix-bug")).ToList();

        findings.Should().ContainSingle()
            .Which.Severity.Should().Be(StartupFindingSeverity.Advisory);
        findings[0].Reason.Should().Contain("fix-bug").And.Contain("retired into 'code'");
    }

    [Fact]
    public void RoutingPipelineNames_SpecDialogInLabelMap_ReportsTheEntry()
    {
        var findings = RoutingPipelineNames.Findings(Config(PipelinePresets.SpecDialogName)).ToList();

        var finding = findings.Should().ContainSingle().Which;
        finding.Severity.Should().Be(StartupFindingSeverity.Advisory,
            "blocking would disable every trigger on the project over one rule");
        finding.Project.Should().Be("alpha");
        finding.Reason.Should().Contain("'bug'").And.Contain($"'{PipelinePresets.SpecDialogName}'");
    }

    [Fact]
    public void RoutingPipelineNames_SpecDialogAsDefault_ReportsIt()
    {
        var config = Config(PipelinePresets.CodeName);
        config.Projects["alpha"].GithubTrigger!.DefaultPipeline = PipelinePresets.SpecDialogName;

        RoutingPipelineNames.Findings(config).Should().ContainSingle()
            .Which.Reason.Should().Contain("default_pipeline").And.Contain(PipelinePresets.SpecDialogName);
    }

    [Fact]
    public void RoutingPipelineNames_RemovedPreset_FindingCarriesReason()
    {
        var findings = RoutingPipelineNames.Findings(Config("autonomous")).ToList();

        findings.Should().ContainSingle()
            .Which.Reason.Should().Contain(RetiredPipelineNames.Explain("autonomous")!,
                "a removed name has no replacement to name, so the finding says why it went");
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
