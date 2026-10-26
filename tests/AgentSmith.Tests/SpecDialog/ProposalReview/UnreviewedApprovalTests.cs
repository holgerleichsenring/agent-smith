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
/// 2026-10-13-8762i: an approval of a proposal with specs not reviewed yet is never an approval — it is held,
/// its recorded decision taken back, and it becomes the edit note that sends those specs to review.
/// </summary>
public sealed class UnreviewedApprovalTests : IDisposable
{
    private const string Platform = "slack";
    private const string Thread = "th-8762i";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;

    public UnreviewedApprovalTests()
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
    public async Task OutcomeConfirmer_ApproveWithNotReviewed_BecomesAnEditNote()
    {
        var (result, _) = await ConfirmAsync([First, ProposalReviewTokens.Series]);

        result.Should().BeOfType<OutcomeEditRequested>().Which.Note.Should().Be(
            $"{SpecDialogUnreviewedApproval.NotePrefix}{First}, the series contract");
    }

    [Fact]
    public async Task OutcomeConfirmer_ConvertedApproval_RecordsNoApprovedDecision()
    {
        var (_, sessions) = await ConfirmAsync([First]);

        var stored = await sessions.GetOpenByThreadAsync(Platform, Thread, CancellationToken.None);
        stored!.Transcript.Should().NotContain(turn => turn.Decision == SpecDialogDecision.Approved,
            "a held approval approved nothing");
        stored.Transcript[^1].Text.Should().Be("approve", "the person's words stay as they were typed");
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
            UnreviewedApprovals.Over(repository), Mock.Of<AgentSmith.Server.Contracts.IOpenDialogQuestions>(), NullLogger<SpecDialogOutcomeConfirmer>.Instance);
        var proposal = new SpecOutcome(new SpecDraftReader().Read(Yaml(First))) { NotReviewed = notReviewed };

        return (await confirmer.ConfirmAsync(state, proposal, CancellationToken.None), sessions);
    }
}
