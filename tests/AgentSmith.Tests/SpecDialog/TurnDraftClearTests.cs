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
/// 2026-11-13-3dc8: a design turn that reviewed a draft and then finished with an answer leaves no draft behind — not
/// in the reviewing column the pane and the next turn read, not in the legacy column, not as the turn's own proposal.
/// </summary>
public abstract class TurnDraftClearStore : IDisposable
{
    protected const string Dialog = "d-3dc8";

    protected readonly SqliteConnection Connection;
    protected readonly AgentSmithDbContext Context;

    protected TurnDraftClearStore()
    {
        Connection = new SqliteConnection("Data Source=:memory:");
        Connection.Open();
        Context = new AgentSmithDbContext(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(Connection).Options);
        MigratedStoreTemplate.CopyInto(Context);
    }

    public void Dispose()
    {
        Context.Dispose();
        Connection.Dispose();
        GC.SuppressFinalize(this);
    }

    internal static BugOutcome Draft(string title) =>
        new BugOutcome(new BugTicketDraft(title, "It vanished.", null)) with { UnderReview = true };

    internal async Task<SpecDialogSession> RowAsync() =>
        await Context.Set<SpecDialogSession>().AsNoTracking().SingleAsync(s => s.ThreadId == Dialog);
}

public sealed class SpecDialogProposalStateTests : TurnDraftClearStore
{
    [Fact]
    public async Task TurnEndsWithoutProposal_ClearsReviewingDraft()
    {
        var harness = new SpecDialogDecisionHarness(Context);
        var state = await harness.OpenAsync("dashboard", Dialog);
        await harness.Proposals.StoreReviewingAsync(state, Draft("removed by the reply"), default);

        await harness.Flow().HandleAsync(state, new AnswerOutcome(), default, runId: "run-a");

        (await harness.Outcomes.ReadAsync("dashboard", Dialog, default)).Reviewing.Should().BeNull();
    }
}

public sealed class SpecDialogProposalClaimTests : TurnDraftClearStore
{
    [Fact]
    public async Task ClearDraft_ReviewingRow_StaysTheTurnsProposalUnderReviewNoMore()
    {
        var harness = new SpecDialogDecisionHarness(Context);
        var state = await harness.OpenAsync("dashboard", Dialog);
        await harness.Proposals.StoreReviewingAsync(state, Draft("a draft"), "run-a", default);

        await new SpecDialogProposalClaim(Context).ClearDraftAsync("dashboard", Dialog, default);

        var row = (await StoredProposals.AllAsync(Context)).Single();
        (row.RunId, row.State).Should().Be(("run-a", (string?)null));
    }

    [Fact]
    public async Task ClearDraft_AwaitingProposal_Kept()
    {
        var harness = new SpecDialogDecisionHarness(Context);
        var state = await harness.OpenAsync("dashboard", Dialog);
        await harness.Proposals.StoreAwaitingAsync(state, new BugOutcome(new BugTicketDraft("awaits", "x", null)), default);
        await harness.Proposals.StoreReviewingAsync(state, Draft("newer draft"), default);

        await new SpecDialogProposalClaim(Context).ClearDraftAsync("dashboard", Dialog, default);

        (await StoredProposals.InStateAsync(Context, SpecDialogProposalClaim.Reviewing)).Should().BeNull();
        (await StoredProposals.InStateAsync(Context, SpecDialogProposalClaim.Awaiting)).Should().NotBeNull("the awaiting proposal is not a draft");
    }
}

public sealed class SpecDialogTurnProposalsTests : TurnDraftClearStore
{
    [Fact]
    public async Task AnswerTurnAfterDraft_ShowsNoDraftOnReload()
    {
        var harness = new SpecDialogDecisionHarness(Context);
        var state = await harness.OpenAsync("dashboard", Dialog);
        // Mid-turn, as DialogProposalObserver writes a draft under review.
        await harness.Proposals.StoreReviewingAsync(state, Draft("draft the reply removed"), "run-a", default);
        await harness.Sessions.AppendTurnAsync("dashboard", Dialog, TranscriptRole.Assistant, "removed it", null, null, default, runId: "run-a");
        await harness.Flow().HandleAsync(state, new AnswerOutcome(), default, runId: "run-a");
        Context.Add(new Run
        {
            Id = "run-a", Project = "sample", Pipeline = "spec-dialog", TicketId = Dialog, Status = RunStatuses.Success,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5), FinishedAt = DateTimeOffset.UtcNow,
        });
        await Context.SaveChangesAsync();

        var current = (await harness.Sessions.GetOpenByThreadAsync("dashboard", Dialog, default))!;
        var view = new SpecDialogSessionView(current.JobId, new SpecDialogProjectView("sample", [], []),
            SpecDialogShownTranscript.Turns(current.Transcript), current.LastActivityAt, []);
        var read = await new SpecDialogTurnProposals(harness.TurnProposals,
            new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()),
            new RunLivenessRepository(Context)).WithAsync(view, current, computing: false, default);

        read.Transcript.Single(t => t.RunId == "run-a").Proposal.Should().BeNull();
        read.InterruptedTurns.Should().BeNull();
        read.RunningProposal.Should().BeNull();
        (await harness.Outcomes.ReadAsync("dashboard", Dialog, default)).Reviewing.Should().BeNull();
    }
}
