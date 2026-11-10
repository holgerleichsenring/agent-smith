using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>
/// 2026-11-10-8e59: an approval never depends on review state. A proposal with specs not reviewed yet is approved
/// like any other, and its recorded decision stands; what is not reviewed is reviewed before it is built.
/// </summary>
public sealed class ApprovalWithoutReviewStateTests : IDisposable
{
    private const string Platform = "slack";
    private const string Thread = "th-8762i";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;

    public ApprovalWithoutReviewStateTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        MigratedStoreTemplate.CopyInto(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Decision_ApproveWithNotReviewed_ClaimsAndFiles()
    {
        var (filed, _) = await ApproveAsync([First, ProposalReviewTokens.Series]);

        filed.Should().BeTrue("the guarantee sits before building, not on the button");
    }

    [Fact]
    public async Task Decision_ApproveWithNotReviewed_KeepsTheApprovedDecision()
    {
        var (_, sessions) = await ApproveAsync([First]);

        var stored = await sessions.GetOpenByThreadAsync(Platform, Thread, CancellationToken.None);
        stored!.Transcript.Should().Contain(turn => turn.Decision == SpecDialogDecision.Approved);
    }

    [Fact]
    public async Task Decision_ApproveWhenAllReviewed_StillFiles()
    {
        var (filed, _) = await ApproveAsync(null);

        filed.Should().BeTrue();
    }

    // 2026-11-10-ccd9: the proposal is stored awaiting by the flow and approved later by its id.
    private async Task<(bool Filed, SpecDialogSessionManager Sessions)> ApproveAsync(IReadOnlyList<string>? notReviewed)
    {
        var sink = new Mock<IOutcomeSink>();
        var harness = new SpecDialogDecisionHarness(_context, new DashboardOutcomeChannel(
            new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()),
            NullLogger<DashboardOutcomeChannel>.Instance), sink.Object);
        var sessions = harness.Sessions;
        var state = await sessions.OpenAsync(Platform, "C1", Thread, "U1",
            new ActiveScope { Project = "sample", Repos = ["repo-a"] }, CancellationToken.None);
        var proposal = new SpecOutcome(new SpecDraftReader().Read(Yaml(First))) { NotReviewed = notReviewed };
        await harness.Flow().HandleAsync(state, proposal, CancellationToken.None);
        var id = (await harness.Outcomes.ReadAsync(Platform, Thread, CancellationToken.None)).ProposalId!;
        await sessions.AppendTurnAsync(Platform, Thread, TranscriptRole.User, "approve", null,
            SpecDialogDecision.Approved, CancellationToken.None);

        await harness.Decisions().DecideAsync(state, SpecDialogDecision.Approved, id, false, CancellationToken.None);

        var filed = sink.Invocations.Any(i => i.Method.Name == nameof(IOutcomeSink.AcceptAsync));
        return (filed, sessions);
    }
}
