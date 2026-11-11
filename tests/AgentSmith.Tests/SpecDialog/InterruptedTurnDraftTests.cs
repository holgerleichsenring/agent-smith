using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-11-11-205ff: every stored per-turn proposal stays in the view. A turn whose replica died left its draft and no
/// kept turn; once its run row is no longer live it shows as an interrupted turn beside the next turn's own — and a
/// draft whose run row is still live shows as the running turn's, whichever replica serves the page.
/// </summary>
public sealed class InterruptedTurnDraftTests : IDisposable
{
    private const string Dialog = "d-205ff";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;

    public InterruptedTurnDraftTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        MigratedStoreTemplate.CopyInto(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task TurnProposals_CrashedTurnThenNextTurn_BothDraftsInTheView()
    {
        var harness = new SpecDialogDecisionHarness(_context);
        var state = await harness.OpenAsync("dashboard", Dialog);
        // Turn A's replica stored its draft and died; the reaper marked its run, as it would.
        await harness.TurnProposals.WriteAsync(state, "run-a", Bug("draft of a") with { UnderReview = true }, default);
        await RunAsync("run-a", RunStatuses.Failed);
        // Turn B ran to completion.
        await harness.Sessions.AppendTurnAsync("dashboard", Dialog, TranscriptRole.Assistant, "b", null, null, default, runId: "run-b");
        await harness.Flow().HandleAsync(state, Bug("b's proposal"), default, runId: "run-b");

        var read = await ReadAsync(harness);

        read.InterruptedTurns.Should().ContainSingle().Which.Should().Match<SpecDialogInterruptedTurnView>(
            t => t.RunId == "run-a" && t.Proposal.Bug!.Title == "draft of a");
        read.Transcript.Single(t => t.RunId == "run-b").Proposal!.Bug!.Title.Should().Be("b's proposal");
        read.RunningProposal.Should().BeNull("no run is live");
    }

    [Fact]
    public async Task TurnProposals_OrphanOfALiveRunOnAnotherReplica_IsRunningNotInterrupted()
    {
        var harness = new SpecDialogDecisionHarness(_context);
        var state = await harness.OpenAsync("dashboard", Dialog);
        await harness.TurnProposals.WriteAsync(state, "run-x", Bug("draft under review") with { UnderReview = true }, default);
        await RunAsync("run-x", RunStatuses.Running);

        // This replica's gate knows nothing of run-x: computing is false here.
        var read = await ReadAsync(harness);

        read.RunningProposal!.Bug!.Title.Should().Be("draft under review", "the run row says it is live");
        read.InterruptedTurns.Should().BeNull();
    }

    [Fact]
    public async Task TurnProposals_TwoLiveOrphansAndADeadOne_OnlyTheDeadOneReadsAsStopped()
    {
        var harness = new SpecDialogDecisionHarness(_context);
        var state = await harness.OpenAsync("dashboard", Dialog);
        await harness.TurnProposals.WriteAsync(state, "run-z", Bug("dead draft"), default);
        await RunAsync("run-z", RunStatuses.Failed);
        await harness.TurnProposals.WriteAsync(state, "run-x", Bug("older live draft"), default);
        await RunAsync("run-x", RunStatuses.Running);
        await harness.TurnProposals.WriteAsync(state, "run-y", Bug("newest live draft"), default);
        await RunAsync("run-y", RunStatuses.Running);

        var read = await ReadAsync(harness);

        read.RunningProposal!.Bug!.Title.Should().Be("newest live draft");
        read.InterruptedTurns!.Select(t => (t.RunId, t.Running)).Should().Equal(("run-z", false), ("run-x", true));
    }

    private async Task<SpecDialogSessionView> ReadAsync(SpecDialogDecisionHarness harness)
    {
        var current = (await harness.Sessions.GetOpenByThreadAsync("dashboard", Dialog, default))!;
        var view = new SpecDialogSessionView(current.JobId, new SpecDialogProjectView("sample", [], []),
            SpecDialogShownTranscript.Turns(current.Transcript), current.LastActivityAt, []);
        return await new SpecDialogTurnProposals(harness.TurnProposals,
            new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()),
            new RunLivenessRepository(_context)).WithAsync(view, current, computing: false, default);
    }

    private async Task RunAsync(string id, string status)
    {
        _context.Add(new Run
        {
            Id = id, Project = "sample", Pipeline = "spec-dialog", TicketId = Dialog, Status = status,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            FinishedAt = status == RunStatuses.Running ? null : DateTimeOffset.UtcNow,
        });
        await _context.SaveChangesAsync();
    }

    private static BugOutcome Bug(string title) => new(new BugTicketDraft(title, "It vanished.", null));
}
