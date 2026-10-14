using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.TicketGraph.TicketGraphRig;

namespace AgentSmith.Tests.SpecDialog.TicketGraph;

/// <summary>
/// 2026-10-14-dbcel: an approved list is filed ticket by ticket in merge order — created, its edges stored, its
/// ticket-text record stored last — and only the complete tickets with nothing left to wait for are started.
/// </summary>
public sealed class TicketGraphFilerTests
{
    private readonly TicketGraphRig _rig = new();

    [Fact]
    public async Task TicketGraphFiler_TwoTickets_FiledInMergeOrderAsWorkTicketsWithTheStamp()
    {
        var report = await _rig.FileAsync(List(Ticket("ui", "api"), Ticket("api")));

        report.Error.Should().BeNull();
        _rig.Tracker.Created.Select(c => c.Title).Should().Equal("Title api", "Title ui");
        _rig.Tracker.Created.Should().AllSatisfy(c => c.Labels.Should().Equal(FiledTicketLabels.ApprovedSetStamp));
        report.Filed.Select(f => f.Key).Should().Equal("#101", "#102");
    }

    [Fact]
    public async Task TicketGraphFiler_Dependent_EdgesStoredBeforeItsApprovalRecord()
    {
        _rig.Approvals.EdgesSoFar = () => _rig.Facts.Edges.Count;

        await _rig.FileAsync(List(Ticket("api"), Ticket("ui", "api")));

        _rig.Approvals.EdgesWhenSaved.Should().Equal([0, 1], "the dependent's edge is stored before its record");
        _rig.Facts.Edges.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Tracker = "sample-tracker", TicketKey = _rig.KeyOf("102"), TicketId = "102", Project = "proj",
            RequiredTicketKey = _rig.KeyOf("101"), RequiredTicketId = "101", FiledInConversation = ConversationId,
        });
    }

    [Fact]
    public async Task TicketGraphFiler_RootStarted_DependentHeldNamingWhatItWaitsFor()
    {
        var report = await _rig.FileAsync(List(Ticket("api"), Ticket("ui", "api")));

        report.Filed[0].Start!.State.Should().Be(FiledStartState.Started);
        report.Filed[1].Start.Should().Be(new FiledWorkStart(
            FiledStartState.Held, "waits for #101 to be merged or marked done by hand"));
        _rig.Tracker.Labels.Should().Equal([("101", Tag)], "a held ticket is never tagged");
        _rig.Tracker.Moves.Should().Equal([("101", "To Do")], "a held ticket is never moved");
    }

    [Fact]
    public async Task TicketGraphFiler_RequiresLandedExistingTicket_StartedAtFiling()
    {
        _rig.Tracker.Existing.Add("41");
        _rig.Land("41");

        var report = await _rig.FileAsync(List(Ticket("api", "ticket:41"), Ticket("ui", "api")));

        report.Filed[0].Start!.State.Should().Be(FiledStartState.Started);
        _rig.Facts.Edges.Select(e => (e.TicketId, e.RequiredTicketId)).Should().Equal(("101", "41"), ("102", "101"));
    }

    [Fact]
    public async Task TicketGraphFiler_RequiresUnlandedExistingTicket_Held()
    {
        _rig.Tracker.Existing.Add("41");

        var report = await _rig.FileAsync(List(Ticket("api", "ticket:#41"), Ticket("ui")));

        report.Filed[0].Start.Should().Be(new FiledWorkStart(FiledStartState.Held, "waits for #41 to be merged or marked done by hand"));
        report.Filed[1].Start!.State.Should().Be(FiledStartState.Started);
    }

    [Fact]
    public async Task TicketGraphFiler_MissingExistingTickets_NothingCreatedAndEveryMissingIdNamed()
    {
        _rig.Tracker.Existing.Add("41");

        var report = await _rig.FileAsync(List(Ticket("api", "ticket:41", "ticket:42"), Ticket("ui", "ticket:43")));

        _rig.Tracker.Created.Should().BeEmpty();
        report.Filed.Should().BeEmpty();
        report.Error.Should().Contain("ticket 42 does not exist").And.Contain("ticket 43 does not exist").And.NotContain("41");
    }

    [Fact]
    public async Task TicketGraphFiler_AzureDevOpsTf401232_IsAMissingIdNotTrackerText()
    {
        var report = await _rig.FileAsync(List(Ticket("api", "ticket:42"), Ticket("ui")));

        report.Error.Should().Be("Nothing was filed: ticket 42 does not exist on sample-tracker.");
        report.Error.Should().NotContain("TF401232");
    }

    [Fact]
    public async Task TicketGraphFiler_UnreadableExistingTicket_RefusedWithItsClass()
    {
        _rig.Tracker.ReadFailures["42"] = new HttpRequestException("401 at https://tracker.internal.example", null, System.Net.HttpStatusCode.Unauthorized);

        var report = await _rig.FileAsync(List(Ticket("api", "ticket:42"), Ticket("ui")));

        report.Error.Should().Be("Nothing was filed: ticket 42 could not be read (unauthorized).");
        _rig.Tracker.Created.Should().BeEmpty();
    }

    [Fact]
    public async Task TicketGraphFiler_SecondTicketCreateFails_FirstStartedIfReady_RestNamedAsNotCreated()
    {
        _rig.Tracker.FailCreateAt = 1;

        var report = await _rig.FileAsync(List(Ticket("api"), Ticket("ui", "api"), Ticket("docs", "ui")));

        report.Error.Should().Be("the tracker refused the create");
        report.Filed.Should().ContainSingle().Which.Start!.State.Should().Be(FiledStartState.Started);
        report.Notes.Should().Equal("Not created: ui — Title ui, docs — Title docs.");
    }

    [Fact]
    public async Task TicketGraphFiler_RecordStoreFailsOnSecondTicket_ItIsNeverTaggedOrMoved()
    {
        _rig.Approvals.FailSaveAt = 1;

        var report = await _rig.FileAsync(List(Ticket("api"), Ticket("ui")));

        report.Error.Should().Contain("the approvals table is gone");
        report.Filed[1].Start.Should().Be(new FiledWorkStart(FiledStartState.NotStarted, TicketGraphStarts.Incomplete));
        _rig.Tracker.Labels.Select(l => l.Ticket).Should().Equal("101");
        _rig.Tracker.Moves.Select(m => m.Ticket).Should().Equal("101");
    }

    [Fact]
    public async Task TicketGraphFiler_EdgeStoreFails_CreatedTicketNamedDoNotTriggerAndNotStarted()
    {
        _rig.Requirements.Fail = true;

        var report = await _rig.FileAsync(List(Ticket("api"), Ticket("ui", "api")));

        report.Error.Should().Contain("the requirements table is gone");
        report.Filed.Select(f => f.Start!.State).Should().Equal(FiledStartState.Started, FiledStartState.NotStarted);
        report.Filed[1].Start!.Reason.Should().Contain("do not trigger it; close it");
        _rig.Approvals.Saved.Should().ContainSingle("the dependent's filing never reached its record");
        _rig.Tracker.Moves.Select(m => m.Ticket).Should().Equal("101");
    }

    [Fact]
    public async Task TicketGraphFiler_CancelledAfterFirstTicket_ReportsAndStartsTheReadyPrefix()
    {
        using var cancel = new CancellationTokenSource();
        _rig.Tracker.AfterCreate = count => { if (count == 1) cancel.Cancel(); };

        var report = await _rig.FileAsync(List(Ticket("api"), Ticket("ui", "api")), ct: cancel.Token);

        report.Error.Should().Be("The filing was interrupted after 1 of 2 tickets.");
        report.Filed.Should().ContainSingle().Which.Start!.State.Should().Be(FiledStartState.Started);
        _rig.Approvals.Saved.Should().ContainSingle("the created ticket's filing completes on no token");
        report.Notes.Should().Equal("Not created: ui — Title ui.");
    }

    [Fact]
    public async Task TicketGraphFiler_CancelledBeforeAnyTicket_Escapes()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        var act = () => _rig.FileAsync(List(Ticket("api"), Ticket("ui", "api")), ct: cancel.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _rig.Tracker.Created.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TicketGraphFiler_StartsWhenMet_CopiedFromMayStartRuns(bool mayStartRuns)
    {
        await _rig.FileAsync(List(Ticket("api"), Ticket("ui", "api"), Ticket("docs", "api", "ui")), mayStartRuns);

        _rig.Facts.Edges.Should().HaveCount(3).And.OnlyContain(e => e.StartsWhenMet == mayStartRuns);
    }

    [Fact]
    public async Task TicketGraphFiler_NoSpecParentStampAndNoBranchWrite()
    {
        await _rig.FileAsync(List(Ticket("api"), Ticket("ui", "api")));

        _rig.Tracker.Created.SelectMany(c => c.Labels).Should().NotContain(l => l.StartsWith(FiledTicketLabels.ParentPrefix));
        _rig.Approvals.Saved.Should().OnlyContain(r => r.TicketText && r.Set.Specs.Count == 0 && r.Set.Series == null,
            "a ticket text carries no series a branch write could publish");
    }

    [Theory]
    [InlineData(TrackerType.AzureDevOps)]
    [InlineData(TrackerType.Jira)]
    [InlineData(TrackerType.GitLab)]
    [InlineData(TrackerType.GitHub)]
    public async Task TicketGraphFiler_EachTrackerType_FilesStoresEdgesAndRecords(TrackerType type)
    {
        var rig = new TicketGraphRig(type);
        rig.Tracker.Existing.Add("40");

        var report = await rig.FileAsync(List(Ticket("api", "ticket:40"), Ticket("ui", "api")));

        report.Error.Should().BeNull();
        report.Filed.Should().HaveCount(2);
        var first = rig.Tracker.Created[0].Id;
        rig.Facts.Edges.Select(e => (e.RequiredTicketKey, e.RequiredTicketId))
            .Should().Equal((rig.KeyOf("40"), "40"), (rig.KeyOf(first), first));
        rig.Approvals.Saved.Select(r => r.Key).Should().Equal(rig.KeyOf(first), rig.KeyOf(rig.Tracker.Created[1].Id));
        report.Filed.Should().AllSatisfy(f => f.Start!.State.Should().Be(FiledStartState.Held));
    }

    [Fact]
    public async Task TicketGraphFiler_MissingExistingTicketOnEachTracker_IsMissing()
    {
        foreach (var type in new[] { TrackerType.Jira, TrackerType.GitLab, TrackerType.GitHub })
        {
            var report = await new TicketGraphRig(type).FileAsync(List(Ticket("api", "ticket:42"), Ticket("ui")));
            report.Error.Should().Be("Nothing was filed: ticket 42 does not exist on sample-tracker.", type.ToString());
        }
    }
}
