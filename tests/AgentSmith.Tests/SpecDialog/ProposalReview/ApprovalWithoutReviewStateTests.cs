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
    public async Task OutcomeConfirmer_ApproveWithNotReviewed_Confirms()
    {
        var (result, _) = await ConfirmAsync([First, ProposalReviewTokens.Series]);

        result.Should().BeOfType<OutcomeConfirmed>("the guarantee sits before building, not on the button");
    }

    [Fact]
    public async Task OutcomeConfirmer_ApproveWithNotReviewed_KeepsTheApprovedDecision()
    {
        var (_, sessions) = await ConfirmAsync([First]);

        var stored = await sessions.GetOpenByThreadAsync(Platform, Thread, CancellationToken.None);
        stored!.Transcript.Should().Contain(turn => turn.Decision == SpecDialogDecision.Approved);
    }

    [Fact]
    public async Task OutcomeConfirmer_ApproveWhenAllReviewed_StillConfirms()
    {
        var (result, _) = await ConfirmAsync(null);

        result.Should().BeOfType<OutcomeConfirmed>();
    }

    private async Task<(ConfirmationResult Result, SpecDialogSessionManager Sessions)> ConfirmAsync(
        IReadOnlyList<string>? notReviewed)
    {
        var repository = new SpecDialogSessionRepository(_context);
        var sessions = new SpecDialogSessionManager(repository, BoundSessionNudges.Quiet(repository),
            AgentSmith.Tests.Sandbox.Holds.None(), TimeProvider.System, NullLogger<SpecDialogSessionManager>.Instance);
        var state = await sessions.OpenAsync(Platform, "C1", Thread, "U1",
            new ActiveScope { Project = "sample", Repos = ["repo-a"] }, CancellationToken.None);
        await sessions.AppendTurnAsync(Platform, Thread, TranscriptRole.User, "approve", null,
            SpecDialogDecision.Approved, CancellationToken.None);
        var transport = new Mock<IDialogueTransport>();
        transport.Setup(t => t.WaitForAnswerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string questionId, TimeSpan _, CancellationToken _) =>
                new DialogAnswer(questionId, "approve", null, DateTimeOffset.UtcNow, "U1"));
        var confirmer = new SpecDialogOutcomeConfirmer(transport.Object,
            new SpecDialogMessenger([], NullLogger<SpecDialogMessenger>.Instance),
            new SpecDialogPendingQuestions(new SpecDialogTurnGate(TimeProvider.System)), new SpecDialogOutcomeComposer(),
            Mock.Of<AgentSmith.Server.Contracts.IOpenDialogQuestions>(), NullLogger<SpecDialogOutcomeConfirmer>.Instance);
        var proposal = new SpecOutcome(new SpecDraftReader().Read(Yaml(First))) { NotReviewed = notReviewed };

        return (await confirmer.ConfirmAsync(state, proposal, CancellationToken.None), sessions);
    }
}
