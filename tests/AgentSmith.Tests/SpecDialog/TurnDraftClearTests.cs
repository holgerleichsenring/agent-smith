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
    public async Task ClearDraft_LegacyReviewingRow_Cleared()
    {
        var harness = new SpecDialogDecisionHarness(Context);
        await harness.OpenAsync("dashboard", Dialog);
        var json = OutcomeProposalJson.Write(Draft("legacy draft"));
        await Context.Set<SpecDialogSession>().Where(s => s.ThreadId == Dialog).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.LatestProposalJson, json).SetProperty(r => r.ReviewingProposalJson, json));

        await new SpecDialogProposalClaim(Context).ClearDraftAsync("dashboard", Dialog, default);

        var row = await RowAsync();
        row.ReviewingProposalJson.Should().BeNull();
        row.LatestProposalJson.Should().BeNull("a row with no id would show it as the legacy fallback");
    }

    [Fact]
    public async Task ClearDraft_AwaitingProposal_Kept()
    {
        var harness = new SpecDialogDecisionHarness(Context);
        var state = await harness.OpenAsync("dashboard", Dialog);
        await harness.Proposals.StoreAwaitingAsync(state, new BugOutcome(new BugTicketDraft("awaits", "x", null)), default);
        await harness.Proposals.StoreReviewingAsync(state, Draft("newer draft"), default);

        await new SpecDialogProposalClaim(Context).ClearDraftAsync("dashboard", Dialog, default);

        var row = await RowAsync();
        row.ReviewingProposalJson.Should().BeNull();
        row.AwaitingProposalJson.Should().NotBeNull();
        row.LatestProposalJson.Should().NotBeNull("the awaiting proposal's mirror is not a draft");
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
        await harness.Proposals.StoreReviewingAsync(state, Draft("draft the reply removed"), default);
        await harness.TurnProposals.WriteAsync(state, "run-a", Draft("draft the reply removed"), default);
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
