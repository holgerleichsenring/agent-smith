using AgentSmith.Application.Services.Polling;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecNaming;

/// <summary>
/// 2026-10-12-967aa: the tracker words a later release writes — spec, spec-epic, spec-parent:,
/// spec:approved — and the config key work_item_kinds.spec route exactly as the phase ones do.
/// The phase words stay read for good: they sit on boards and in configs this framework does not own.
/// </summary>
public sealed class SpecNamedTrackerReadTests
{
    private static readonly TicketLabelVocabulary Vocabulary = TicketLabelVocabulary.Default;

    [Fact]
    public void DiscoveryLabelGuard_SpecAndPhaseLabels_BothRoute()
    {
        var trigger = new WebhookTriggerConfig
        {
            TriggerStatuses = ["To Do"],
            PipelineFromLabel = new Dictionary<string, string> { ["bug"] = "fix-bug" },
        };

        var guard = DiscoveryLabelGuard.For([trigger], new TrackerConnection { Name = "t", Type = TrackerType.Jira });

        guard.Should().Contain(["phase", "spec", "phase-spec:approved", "spec:approved"]);
    }

    [Theory]
    [InlineData("phase")]
    [InlineData("spec")]
    [InlineData("phase-spec:approved")]
    [InlineData("spec:approved")]
    [InlineData("Spec:Approved")]
    public void BindsSpecExecution_EitherWord_Binds(string label) =>
        FiledTicketLabels.BindsPhaseExecution(Envelope(label), Vocabulary).Should().BeTrue();

    [Fact]
    public void BindsSpecExecution_AnOrdinaryLabel_DoesNotBind() =>
        FiledTicketLabels.BindsPhaseExecution(Envelope("specification"), Vocabulary).Should().BeFalse();

    [Theory]
    [InlineData("phase-epic")]
    [InlineData("spec-epic")]
    public void IsEpicRecord_EitherRecordLabel_IsARecord(string label) =>
        FiledTicketLabels.IsEpicRecord(Envelope(label)).Should().BeTrue();

    [Theory]
    [InlineData("phase-parent:4711")]
    [InlineData("spec-parent:4711")]
    public void ParentId_EitherStamp_NamesTheParent(string label) =>
        FiledTicketLabels.ParentId([label]).Should().Be("4711");

    [Fact]
    public void Vocabulary_SpecStamp_IsOursOnAConfiguredBoardToo() =>
        new TicketLabelVocabulary(new Dictionary<string, string> { ["approved-set"] = "approved" })
            .IsApprovedSetStamp("spec:approved").Should().BeTrue();

    [Theory]
    [InlineData("spec")]
    [InlineData("phase")]
    [InlineData("SPEC")]
    public void TicketKindResolver_ConfigKeySpec_MapsToSpecRole(string key)
    {
        var project = new ResolvedProject
        {
            Name = "p1",
            Tracker = new TrackerConnection { Name = "t1", WorkItemKinds = new Dictionary<string, string> { [key] = "User Story" } },
        };

        new TicketKindResolver(NullLogger<TicketKindResolver>.Instance).For(project, TicketFilingRole.Phase)
            .Should().Be("User Story");
    }

    private static IncomingTicketEnvelope Envelope(string label) => new() { Labels = [label] };
}
