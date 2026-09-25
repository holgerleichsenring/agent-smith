using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-25-3c7aa: the approval RECORD is what says a ticket has a specification somebody
/// approved; the label that used to say it is one of three ways to reach the same answer and can
/// be deleted by anyone with tracker access.
/// <para>
/// Both diagonals are pinned, because only one of them is new: a record with no label now binds
/// and is held to the missing-set park, while a label with no record keeps doing exactly what it
/// did — a run in a process that binds the in-memory store is handed no record at all, and
/// replacing the label check would have deleted the bind for every one of them.
/// </para>
/// </summary>
public sealed class ApprovedRecordBindsTests
{
    private readonly FiledTicketSpecGate _gate = new(NullLogger<FiledTicketSpecGate>.Instance);

    [Fact]
    public async Task Probe_APlatformWithTwoConnections_FindsTheRecordOnEither()
    {
        var store = new AgentSmith.Application.Services.Persistence.InMemorySpecApprovalStore();
        var key = SpecSetKey.For("jira", "DPG-1239");
        await store.SaveAsync(ApprovedSets.Record(key.Value, ApprovedSets.Noon, tracker: "second-jira"), default);
        var probe = new ApprovedRecordProbe(store, NullLogger<ApprovedRecordProbe>.Instance);

        var found = await probe.ExistsForPlatformAsync(
            TwoJiraConnections(), "jira", "DPG-1239", default);

        found.Should().BeTrue(
            "a webhook route is per platform — two connections share one endpoint, so the record "
            + "is looked for on both");
    }

    [Fact]
    public async Task Probe_APlatformWithNoRecordAnywhere_AnswersNo()
    {
        var probe = ApprovedRecordProbes.None();

        var found = await probe.ExistsForPlatformAsync(
            TwoJiraConnections(), "jira", "DPG-1239", default);

        found.Should().BeFalse();
    }

    [Fact]
    public void Gate_ARecordAndAnUnreadableBranch_Parks()
    {
        var park = _gate.MissingSet(
            TicketWith(), Key, ApprovedSets.Record(Key.Value, ApprovedSets.Noon),
            SpecSetBranchState.Unreadable);

        park.Should().NotBeNull(
            "the record holds the ticket to the same rule the stamp did — and this is the arm the "
            + "hand-off does not answer first");
    }

    [Fact]
    public void Gate_AStampWithNoRecordAndNoBranch_ParksAndSaysWhichHalfIsMissing()
    {
        var park = _gate.MissingSet(
            TicketWith(FiledTicketLabels.ApprovedSetStamp), Key, record: null,
            SpecSetBranchState.NothingAtThePath);

        park!.Reason.Should().Contain(FiledTicketLabels.ApprovedSetStamp,
            "with no record to name, the sentence falls back to the label that held it");
        park.Reason.Should().Contain("nobody approved it");
    }

    [Fact]
    public void Gate_ARecordWithNoStamp_IsNamedByTheRecordAndNotByALabelItDoesNotCarry()
    {
        var park = _gate.MissingSet(
            TicketWith(), Key, ApprovedSets.Record(Key.Value, ApprovedSets.Noon),
            SpecSetBranchState.Unreadable);

        park!.Reason.Should().NotContain(FiledTicketLabels.ApprovedSetStamp,
            "asserting a label this ticket does not carry is the sentence this phase removed");
        park.Reason.Should().Contain("recorded for it");
    }

    [Fact]
    public void Gate_AnEpicChild_IsStillExemptBeforeTheParkIsConsidered()
    {
        var park = _gate.MissingSet(
            TicketWith(FiledTicketLabels.ParentStamp("42")), Key,
            ApprovedSets.Record(Key.Value, ApprovedSets.Noon),
            SpecSetBranchState.NothingAtThePath);

        park.Should().BeNull("an epic child carries no spec by design and still derives");
    }

    [Fact]
    public void Gate_AHandWrittenTicketWithNeither_DerivesAsBefore()
    {
        var park = _gate.MissingSet(
            TicketWith(), Key, record: null, SpecSetBranchState.NothingAtThePath);

        park.Should().BeNull();
    }

    private static readonly SpecSetKey Key = new("jira-1");

    private static Ticket TicketWith(params string[] labels) =>
        new(new TicketId("1"), "A ticket", "Body", null, "Open", "jira", labels);

    private static AgentSmithConfig TwoJiraConnections() => new()
    {
        Trackers = new Dictionary<string, TrackerConnection>
        {
            ["first-jira"] = new() { Name = "first-jira", Type = TrackerType.Jira },
            ["second-jira"] = new() { Name = "second-jira", Type = TrackerType.Jira },
        },
    };

}
