using System.Text.Json;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets.Checks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static AgentSmith.Tests.Tickets.TicketConversationFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets;

/// <summary>
/// 2026-10-13-9a59h: Stuck is observed, never assumed — the between-attempts check reads the tracker only
/// after a failed attempt and at most every five minutes, the list reads the stored row and only kicks the
/// check, and a person's act on the tracker or the pull request takes a ticket out of stuck.
/// </summary>
public sealed class TicketStuckTests : IDisposable
{
    private static readonly DateTimeOffset Now = T.AddHours(3);
    private static readonly Ticket Text = new(new TicketId("42"), "Ticket 42", "Export it.", "In a second.", "Open", "github");

    private readonly TicketStoreHarness _h = new();

    public TicketStuckTests()
    {
        _h.Tracker.Setup(t => t.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>())).ReturnsAsync(Text);
        _h.Tracker.Setup(t => t.GetCommentsAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<TicketComment>());
    }

    public void Dispose() => _h.Dispose();

    private Task FailedAsync(string ticket = "42", string id = "r-1")
    {
        var run = Run(id, ticket, status: "failed");
        run.ActsReadAt = T;
        return _h.AddAsync(run, Step(id), new RunAttempt { RunId = id, TicketFingerprint = TicketTextFingerprint.Of(Text) });
    }

    private static TicketBetweenAttemptsCheck Check(DateTimeOffset at, bool unchanged = true, string acts = "[]", string runId = "r-1") =>
        new() { RunId = runId, CheckedAt = at, FingerprintUnchanged = unchanged, ActsJson = acts };

    private async Task<TicketSummaryView> RowAsync() =>
        (await _h.List.ReadAsync(new TicketListRequest("alpha", null, null, null), default)).Tickets.Single();

    private async Task<int> ChecksAsync()
    {
        await using var scope = _h.Services.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<AgentSmith.Infrastructure.Persistence.Contracts.IUnitOfWork>();
        return uow.Set<TicketBetweenAttemptsCheck>().Count();
    }

    [Fact]
    public async Task TicketStuck_FailedUnchangedNoActChecked_IsStuck()
    {
        await FailedAsync();

        await _h.Detail.ReadAsync("alpha", "42", default);
        var list = await _h.List.ReadAsync(new TicketListRequest("alpha", null, null, null), default);

        var row = list.Tickets.Single();
        (row.Bucket, row.UnchangedSinceLastAttempt, row.CheckedAt).Should().Be((TicketBucket.Stuck, (bool?)true, (DateTimeOffset?)Now));
        list.Counts.Stuck.Should().Be(1);
    }

    [Fact]
    public async Task TicketTrackerObserver_LabelNoteOnAnOtherwiseUnchangedTicket_ReadsUnchanged()
    {
        await FailedAsync();
        var noted = new Ticket(Text.Id, Text.Title, $"{Text.Description}\n\n{TicketLabelNote.Begin}\nbookkeeping\n{TicketLabelNote.End}",
            Text.AcceptanceCriteria, Text.Status, Text.Source);
        _h.Tracker.Setup(t => t.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>())).ReturnsAsync(noted);

        await _h.Detail.ReadAsync("alpha", "42", default);

        (await RowAsync()).UnchangedSinceLastAttempt.Should().BeTrue("our own label note is not an edit of the ticket");
    }

    [Fact]
    public async Task TicketStuck_NoCheckOrTrackerDown_IsUnknownNotStuck()
    {
        await FailedAsync();
        (await RowAsync()).Should().Match<TicketSummaryView>(r => r.Bucket == TicketBucket.Failed && r.UnchangedSinceLastAttempt == null);

        _h.Tracker.Setup(t => t.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("down"));
        await _h.Detail.ReadAsync("alpha", "42", default);

        (await ChecksAsync()).Should().Be(0, "a failed read writes nothing");
        (await RowAsync()).Bucket.Should().Be(TicketBucket.Failed);
    }

    [Fact]
    public async Task TicketStuck_PersonPrReviewAfterCheck_IsNotStuck()
    {
        await FailedAsync();
        await _h.AddAsync(Check(Now), Record("7", PrReviewStates.Unread), Note("7", "c-1", "Still broken.", 40, author: "bob"));

        var row = await RowAsync();
        var detail = await _h.Detail.ReadAsync("alpha", "42", default);

        (row.Bucket, row.UnchangedSinceLastAttempt).Should().Be((TicketBucket.Failed, (bool?)false));
        detail!.ActedSince.Should().ContainSingle().Which.Should().Be(new TicketActedSinceView("pr-review", "bob", T.AddMinutes(40), PrUrl("7")));
    }

    [Fact]
    public async Task TicketList_ReadsCheckRow_NeverTheTracker()
    {
        await FailedAsync();
        await _h.AddAsync(Check(Now.AddMinutes(-30)));

        (await RowAsync()).Bucket.Should().Be(TicketBucket.Stuck);
        _h.Trackers.Verify(f => f.Create(It.IsAny<AgentSmith.Contracts.Models.Configuration.TrackerConnection>()), Times.Never);
    }

    [Fact]
    public async Task TicketDetail_LiveAttempt_ReadsNoTracker()
    {
        await _h.AddAsync(Run("r-1", status: "running"), Step("r-1"));

        await _h.Detail.ReadAsync("alpha", "42", default);

        _h.Trackers.Verify(f => f.Create(It.IsAny<AgentSmith.Contracts.Models.Configuration.TrackerConnection>()), Times.Never);
    }

    [Fact]
    public async Task BetweenAttemptsCheck_YoungerThanFiveMinutes_Reused()
    {
        await FailedAsync();
        await _h.AddAsync(Check(Now.AddMinutes(-4)));

        await _h.Checker.CheckAsync("alpha", "42", default);

        _h.Tracker.Verify(t => t.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BetweenAttemptsCheck_ConcurrentWriters_OneRow()
    {
        await FailedAsync();
        var held = new TaskCompletionSource<Ticket>();
        var reads = 0;
        _h.Tracker.Setup(t => t.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .Returns(() => Interlocked.Increment(ref reads) == 1 ? held.Task : Task.FromResult(Text));

        var first = _h.Checker.CheckAsync("alpha", "42", default);
        await _h.Checker.CheckAsync("alpha", "42", default);
        held.SetResult(Text);
        await first;

        (await ChecksAsync()).Should().Be(1, "both replicas checked the same last attempt and RunId is unique");
    }

    [Fact]
    public async Task BetweenAttemptsCheck_Changed_PublishesTicketActivity()
    {
        await FailedAsync();
        await _h.Checker.CheckAsync("alpha", "42", default);
        _h.Activity.Verify(a => a.ChangedAsync("alpha", "github-42", It.IsAny<CancellationToken>()), Times.Once);

        await _h.AddAsync(Check(Now.AddMinutes(-10), runId: "r-x"));
        await using (var scope = _h.Services.CreateAsyncScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<AgentSmith.Infrastructure.Persistence.Contracts.IUnitOfWork>();
            uow.Set<TicketBetweenAttemptsCheck>().Single(c => c.RunId == "r-1").CheckedAt = Now.AddMinutes(-10);
            await uow.SaveChangesAsync();
        }
        await _h.Checker.CheckAsync("alpha", "42", default);

        _h.Activity.Verify(a => a.ChangedAsync("alpha", "github-42", It.IsAny<CancellationToken>()), Times.Once,
            "the same observation again is no change");
    }

    [Fact]
    public async Task TicketNeedsYou_Handback_SinceIsRecordedTime()
    {
        await _h.AddAsync(Run("r-1", status: "waiting_for_input"), Step("r-1"),
            Row("r-1", 4, RunConversationKinds.HandBack, 20, "## Agent Smith — refused"));

        var list = await _h.List.ReadAsync(new TicketListRequest("alpha", null, null, null), default);

        (list.Tickets.Single().Kind, list.Tickets.Single().Since).Should().Be((TicketNeedsYouKind.Handback, (DateTimeOffset?)T.AddMinutes(20)));
        list.Counts.OldestNeedsYouSince.Should().Be(T.AddMinutes(20));
    }

    [Fact]
    public async Task TicketSummary_Handback_CarriesTextAndAt()
    {
        await _h.AddAsync(Run("r-1", status: "waiting_for_input"), Step("r-1"),
            Row("r-1", 4, RunConversationKinds.HandBack, 20, "first words"), Row("r-1", 9, RunConversationKinds.HandBack, 25, "the words"));

        (await RowAsync()).Handback.Should().Be(new TicketHandbackView("the words", T.AddMinutes(25)));
        (await _h.Detail.ReadAsync("alpha", "42", default))!.Summary.Handback!.Text.Should().Be("the words");
    }

    [Fact]
    public async Task TicketBucket_FailedWithOpenReview_IsWaitingOnReviewNotStuck()
    {
        await FailedAsync();
        await _h.AddAsync(Check(Now), Record("7", PrReviewStates.Open));

        (await RowAsync()).Bucket.Should().Be(TicketBucket.WaitingOnReview);
    }

    [Fact]
    public async Task TicketList_FailedTicketsInPage_KickAtMostTenChecksWithoutWaiting()
    {
        _h.Tracker.Setup(t => t.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .Returns(new TaskCompletionSource<Ticket>().Task);
        for (var i = 10; i < 22; i++) await FailedAsync($"{i}", $"r-{i}");

        var list = await _h.List.ReadAsync(new TicketListRequest("alpha", null, null, null), default);

        list.Tickets.Should().HaveCount(12);
        _h.Kicks.Reader.Count.Should().Be(TicketCheckKicks.PerRead);
    }

    [Fact]
    public async Task TicketList_SameTicketWithinFiveMinutes_KickedOnce()
    {
        await FailedAsync();

        await RowAsync();
        await RowAsync();

        _h.Kicks.Reader.Count.Should().Be(1);
    }

    [Fact]
    public async Task TicketCheckKickWorker_KickedTicket_IsChecked()
    {
        await FailedAsync();

        await ActivatorUtilities.CreateInstance<TicketCheckKickWorker>(_h.Services)
            .RunAsync(new TicketCheckKick("alpha", "42"), default);

        (await ChecksAsync()).Should().Be(1);
    }

    [Fact]
    public async Task TicketStuck_CommentOnTicket_RecordedAsActNotStuck()
    {
        await FailedAsync();
        _h.Tracker.Setup(t => t.GetCommentsAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Comment("c-1", "carol", 50, "Try again with main.")]);

        await _h.Detail.ReadAsync("alpha", "42", default);

        var row = await RowAsync();
        (row.Bucket, row.UnchangedSinceLastAttempt).Should().Be((TicketBucket.Failed, (bool?)false));
        (await _h.Detail.ReadAsync("alpha", "42", default))!.ActedSince.Should().ContainSingle()
            .Which.Should().Be(new TicketActedSinceView(TicketTrackerAct.Comment, "carol", T.AddMinutes(50)));
    }

    [Fact]
    public async Task RunDeletion_RemovesBetweenAttemptsCheck()
    {
        await FailedAsync();
        await _h.AddAsync(Check(Now));

        await using (var scope = _h.Services.CreateAsyncScope())
            await new AgentSmith.Infrastructure.Persistence.Repositories.RunDeletionRepository(
                scope.ServiceProvider.GetRequiredService<AgentSmith.Infrastructure.Persistence.Contracts.IUnitOfWork>())
                .DeleteAsync("r-1", CancellationToken.None);

        (await ChecksAsync()).Should().Be(0);
    }
}
