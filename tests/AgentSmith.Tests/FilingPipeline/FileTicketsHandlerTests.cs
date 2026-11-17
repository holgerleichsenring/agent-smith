using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Filing;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using AgentSmith.Tests.SpecDialog.TicketGraph;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.SpecDialog.TicketGraph.TicketGraphRig;

namespace AgentSmith.Tests.FilingPipeline;

/// <summary>
/// 2026-11-17-5701b: the 'file' run's steps over a fake tracker that routes by tag and starts in "To Do" — what each
/// ticket is created with, what is recorded and written for it, and what it became. A list ticket's own repositories on
/// its record: GraphTicketFilingPartsTests.
/// </summary>
public sealed class FileTicketsHandlerTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private readonly TicketGraphRig _rig = new();

    [Fact]
    public async Task FileTickets_Spec_StampedRecordedAndWrittenToItsBranch()
    {
        var report = await _rig.Filer().FileAsync(State(), new SpecOutcome(SpecOf("api")), true, CancellationToken.None);

        report.Error.Should().BeNull();
        _rig.Tracker.Created.Single().Labels.Should().Equal(ApprovedSetTicketFiler.Labels(_rig.Project));
        _rig.Approvals.Saved.Should().ContainSingle().Which.Set.Specs.Should().ContainSingle();
        _rig.Sources.Writes.Should().ContainSingle().Which.Repo.Should().Be("sample-api");
    }

    [Fact]
    public async Task FileTickets_Bug_NoLabelNoRecordNoBranch_AndStillStarted()
    {
        var report = await _rig.Filer().FileAsync(
            State(), new BugOutcome(new BugTicketDraft("Session drops", "Users are signed out.", null)), true, CancellationToken.None);

        _rig.Tracker.Created.Single().Labels.Should().BeEmpty("a bug carries no framework label");
        _rig.Approvals.Saved.Should().BeEmpty();
        _rig.Sources.Writes.Should().BeEmpty();
        report.Filed.Single().Start!.State.Should().Be(FiledStartState.Started, "its own routing tag starts it");
    }

    [Fact]
    public async Task FileTickets_List_RootStartedAndDependentHeld()
    {
        var report = await _rig.FileAsync(List(Ticket("api"), Ticket("ui", "api")));

        report.Filed.Select(f => f.Start!.State).Should().Equal(FiledStartState.Started, FiledStartState.Held);
        report.Filed[1].Start!.Reason.Should().Be("waits for #101 to be merged or marked done by hand");
    }

    [Fact]
    public async Task FileTickets_Record_CarriesTheOwnerWordsWithoutTheApprovalTurn()
    {
        var state = State() with
        {
            Transcript =
            [
                new TranscriptTurn(TranscriptRole.User, "Page size 50, please.", Noon, Author: "Sample Operator"),
                new TranscriptTurn(TranscriptRole.Assistant, "Proposal: 50.", Noon.AddMinutes(1)),
                new TranscriptTurn(TranscriptRole.User, "approve", Noon.AddMinutes(2), Decision: SpecDialogDecision.Approved,
                    DecisionProposalId: "prop-1"),
            ],
        };

        await _rig.Filer().RunAsync(state, List(Ticket("api")), true, "prop-1");

        _rig.Approvals.Saved.Single().OwnerWords.Should().ContainSingle().Which.Text.Should().Be("Page size 50, please.");
    }

    [Fact]
    public async Task FileTickets_FailureAfterTheFirstTicket_ReturnsOkWithTheRestNamed()
    {
        _rig.Tracker.FailCreateAt = 1;
        var filer = _rig.Filer();

        var report = await filer.FileAsync(State(), List(Ticket("api"), Ticket("ui", "api")), true, CancellationToken.None);

        filer.Filed!.IsSuccess.Should().BeTrue("a ticket exists, so the step reports instead of failing");
        report.Error.Should().Be("the tracker refused the create");
        report.Notes.Should().Contain("Not created: ui — Title ui.");
        report.Filed.Single().Start!.State.Should().Be(FiledStartState.Started);
    }

    [Fact]
    public async Task FileTickets_FailureBeforeAnyTicket_FailsTheStep()
    {
        _rig.Tracker.FailCreateAt = 0;
        var filer = _rig.Filer();

        var report = await filer.FileAsync(State(), new SpecOutcome(SpecOf("api")), true, CancellationToken.None);

        filer.Filed!.IsSuccess.Should().BeFalse();
        report.Filed.Should().BeEmpty();
    }

    [Fact]
    public async Task FileTickets_RunCancelledAfterTheFirstTicket_StillStartsTheRoot()
    {
        using var cancel = new CancellationTokenSource();
        _rig.Tracker.AfterCreate = count => { if (count == 1) cancel.Cancel(); };

        var report = await _rig.FileAsync(List(Ticket("api"), Ticket("ui", "api")), ct: cancel.Token);

        report.Filed.Single().Start!.State.Should().Be(FiledStartState.Started);
        report.Error.Should().Be("The filing was interrupted after 1 of 2 tickets.");
    }

    [Fact]
    public async Task PlanFiling_RunWithoutARequest_FailsWithAMessageAndNeverThrows()
    {
        var handler = new PlanFilingHandler(AgentSmith.Tests.TestSupport.FilingDoubles.Planner(Moq.Mock.Of<AgentSmith.Contracts.Providers.ITicketProviderFactory>()),
            NullLogger<PlanFilingHandler>.Instance);

        var result = await handler.ExecuteAsync(new PlanFilingContext(new PipelineContext(), _rig.Project), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain(nameof(ContextKeys.FilingRequest)).And.Contain("Nothing was filed");
    }
}
