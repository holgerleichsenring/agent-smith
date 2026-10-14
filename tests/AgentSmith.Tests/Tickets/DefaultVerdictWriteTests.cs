using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets.Defaults;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;
using static AgentSmith.Tests.Tickets.TicketDefaultsFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets;

/// <summary>
/// 2026-10-13-8762p: POST /api/tickets/{project}/{ticketId}/defaults/verdicts — validated whole before anything is
/// written, one conditional update per item, one rework nudge and one page refresh per batch.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class DefaultVerdictWriteTests : IDisposable
{
    private readonly TicketStoreHarness _store = new();

    public void Dispose() => _store.Dispose();

    private Task SeedAsync() => SeedTwoAttemptsAsync(_store, TicketConversationFixtures.Session(), Run(Scan, pipeline: "security-scan"),
        Default(1, R1, 1), Default(2, R2, 2, holdsSeries: true), Default(3, DesignRun, 0, conversation: Session),
        Default(4, Scan, 3), Default(5, R2, 4, verdict: DefaultDecisionVerdicts.Overruled), Run("2026-10-13T13-00-00-0009", ticket: "7"),
        Default(9, "2026-10-13T13-00-00-0009", 1));

    private async Task<IReadOnlyDictionary<long, RunDefaultDecision>> RowsAsync() =>
        (await _store.ReadAllAsync<RunDefaultDecision>()).ToDictionary(r => r.Id);

    private async Task NothingWrittenAsync()
    {
        (await RowsAsync()).Values.Count(r => r.Verdict == DefaultDecisionVerdicts.Pending).Should().Be(5);
        _store.Nudges.VerifyNoOtherCalls();
        _store.Activity.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DefaultVerdicts_ForeignId_Is400_AndWritesNothing()
    {
        await SeedAsync();

        var refusal = Refusal(await PostAsync(_store, "ana", Ratify(1), Ratify(9)));

        refusal.Should().Contain("item 2 (default #9): this ticket lists no such default").And.Contain("Nothing was written");
        await NothingWrittenAsync();
    }

    [Fact]
    public async Task DefaultVerdicts_UnknownVerdictOrLongNote_Is400()
    {
        await SeedAsync();

        Refusal(await PostAsync(_store, "ana", new DefaultVerdictRequest(1, "maybe"))).Should().Contain("'maybe'");
        Refusal(await PostAsync(_store, "ana", Overrule(1, new string('x', 2001)))).Should().Contain("2001 characters");
        Refusal(await PostAsync(_store, "ana")).Should().Contain("1 to 100 items");
        await NothingWrittenAsync();
    }

    [Fact]
    public async Task DefaultVerdicts_OverruleOfAConversationRow_Is400_AnswerItInTheDesignConversation()
    {
        await SeedAsync();

        Refusal(await PostAsync(_store, "ana", Overrule(3, "no"))).Should().Contain(DefaultVerdictValidation.AnswerInConversation);
        await NothingWrittenAsync();
        (await DecideAsync(_store, "ana", Ratify(3))).Applied.Should().Equal(3L);
    }

    [Fact]
    public async Task DefaultVerdicts_OverruleOfANonAttemptRow_Is400()
    {
        await SeedAsync();

        Refusal(await PostAsync(_store, "ana", Overrule(4))).Should().Contain(DefaultVerdictValidation.OnlyAnAttempt);
        await NothingWrittenAsync();
    }

    [Fact]
    public async Task DefaultVerdicts_Ratify_SetsVerdictByAndAt()
    {
        await SeedAsync();

        (await DecideAsync(_store, "ana", Ratify(1, "  fine  "))).Applied.Should().Equal(1L);

        var row = (await RowsAsync())[1];
        (row.Verdict, row.VerdictBy, row.VerdictAt, row.VerdictNote).Should().Be(
            (DefaultDecisionVerdicts.Ratified, "ana", T.AddHours(3), "fine"));
    }

    [Fact]
    public async Task DefaultVerdicts_DecidedRow_IsAlreadyDecided_NotOverwritten()
    {
        await SeedAsync();

        var result = await DecideAsync(_store, "ana", Ratify(5), Ratify(1));

        result.Applied.Should().Equal(1L);
        result.AlreadyDecided.Should().Equal(new DefaultVerdictStanding(5, DefaultDecisionVerdicts.Overruled));
        (await RowsAsync())[5].VerdictBy.Should().Be("earlier");
    }

    [Fact]
    public async Task DefaultVerdicts_TwoWritersOneRow_ExactlyOneApplies()
    {
        await SeedAsync();

        var results = new[]
        {
            await DecideAsync(_store, "ana", Overrule(1, "use the queue")),
            await DecideAsync(_store, "ben", Ratify(1)),
        };

        results.Sum(r => r.Applied.Count).Should().Be(1);
        results[1].AlreadyDecided.Should().Equal(new DefaultVerdictStanding(1, DefaultDecisionVerdicts.Overruled));
        var row = (await RowsAsync())[1];
        (row.Verdict, row.VerdictBy, row.VerdictNote).Should().Be((DefaultDecisionVerdicts.Overruled, "ana", "use the queue"));
    }

    [Fact]
    public async Task DefaultVerdicts_Overrule_QueuesOneOperatorNudgeWithOverruleChannel()
    {
        await SeedAsync();

        await DecideAsync(_store, "ana", Overrule(1, "no"), Ratify(2));

        var expected = new ReworkNudgeRequest("alpha", "42", ReworkNudgeOrigin.Operator, Channel: ReworkChannel.Overrule);
        _store.Nudges.Verify(n => n.EnqueueAsync(expected, It.IsAny<CancellationToken>()), Times.Once);
        _store.Nudges.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DefaultVerdicts_RatifyOfHoldsSeriesRow_QueuesANudge_PlainRatifyDoesNot()
    {
        await SeedAsync();

        await DecideAsync(_store, "ana", Ratify(1));
        _store.Nudges.VerifyNoOtherCalls();

        await DecideAsync(_store, "ana", Ratify(2));
        _store.Nudges.Verify(n => n.EnqueueAsync(new ReworkNudgeRequest("alpha", "42", ReworkNudgeOrigin.Operator),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DefaultVerdicts_Applied_PublishesOneTicketActivityChanged()
    {
        await SeedAsync();

        await DecideAsync(_store, "ana", Ratify(1), Ratify(2), Ratify(3));
        await DecideAsync(_store, "ana", Ratify(1));

        _store.Activity.Verify(a => a.ChangedAsync("alpha", "github-42", It.IsAny<CancellationToken>()), Times.Once);
        _store.Activity.VerifyNoOtherCalls();
    }
}
