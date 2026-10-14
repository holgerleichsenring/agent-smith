using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static AgentSmith.Tests.Rework.Records.PrReviewRefreshHarness;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>2026-10-13-9a59b: the nudge worker's refresh of a ticket's pull-request records.</summary>
public sealed class PrReviewRecordRefreshTests : IDisposable
{
    private readonly PrReviewRefreshHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task PrReviewRecordRefresh_UpsertsEntriesOncePerHostId()
    {
        await _h.RunAsync("run-1", Pr1);
        _h.State(Pr1, PrReviewStates.Open);
        _h.Threads([new PrReviewThread("a.cs", 3, false, [Note("C1", "alice", "rename"), Note("C2", "bob", "agreed")]) { HostId = "T1" }]);

        await _h.RefreshAsync();
        _h.Clock.Now += TimeSpan.FromMinutes(1);
        await _h.RefreshAsync();

        _h.Records().Should().ContainSingle().Which.State.Should().Be(PrReviewStates.Open);
        _h.Entries().Select(e => (e.HostId, e.ThreadHostId, e.File, e.Line)).Should().Equal(("C1", "T1", "a.cs", 3), ("C2", "T1", "a.cs", 3));
    }

    [Fact]
    public async Task PrReviewEntry_DecisionAndReviewBody_OneRowPerHostId()
    {
        await _h.RunAsync("run-1", Pr1);
        _h.State(Pr1, PrReviewStates.Open,
            Decision("R1", "alice", PrReviewDecisions.ChangesRequested), Decision("R2", "bob", PrReviewDecisions.Approved));
        _h.Threads([new PrReviewThread(null, null, null, [Note("R1", "alice", "please fix")]) { HostId = "R1", FromReview = true }]);

        await _h.RefreshAsync();

        var entries = _h.Entries();
        entries.Should().HaveCount(2);
        entries.Single(e => e.HostId == "R1").Should().Match<PrReviewEntry>(e =>
            e.Kind == PrReviewEntryKinds.Review && e.Decision == PrReviewDecisions.ChangesRequested && e.Standing && e.Body == "please fix");
        entries.Single(e => e.HostId == "R2").Should().Match<PrReviewEntry>(e =>
            e.Kind == PrReviewEntryKinds.Decision && e.Decision == PrReviewDecisions.Approved && e.Standing,
            "an approval with an empty body has no note, so it is a decision row of its own");
    }

    [Fact]
    public async Task PrReviewEntry_SystemNote_StoredWithIsSystem()
    {
        await _h.RunAsync("run-1", Pr1);
        _h.State(Pr1, PrReviewStates.Open, Decision("7/1", "alice", PrReviewDecisions.ChangesRequested));
        _h.Threads([new PrReviewThread(null, null, null, [Note("7/1", "alice", "alice voted -5", isSystem: true)]) { HostId = "7" }]);

        await _h.RefreshAsync();

        _h.Entries().Single().Should().Match<PrReviewEntry>(e => e.IsSystem && e.Standing && e.Trusted,
            "the vote's note is written by the host, the decision is the trusted reviewer's");
    }

    [Fact]
    public async Task PrReviewRecordRefresh_RecordNoRunNames_IsDeleted()
    {
        await _h.RunAsync("run-1", Pr1);
        _h.State(Pr1, PrReviewStates.Open);
        await using (var ctx = _h.Store.Context())
        {
            ctx.Add(new PrReviewRecord { PrKey = PrKey.Of(Pr2), Url = Pr2, Project = "p", TicketId = "7", RunId = "run-0", State = "open" });
            ctx.Add(new PrReviewEntry { PrKey = PrKey.Of(Pr2), HostId = "x", Kind = "comment" });
            await ctx.SaveChangesAsync();
        }

        await _h.RefreshAsync();

        _h.Records().Select(r => r.Url).Should().Equal(Pr1);
        _h.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task PrReviewRecordRefresh_SupersededPr_StillReachesClosed()
    {
        await _h.RunAsync("run-1", Pr1);
        await _h.RunAsync("run-2", Pr2);
        _h.State(Pr1, PrReviewStates.Closed);
        _h.State(Pr2, PrReviewStates.Open);

        await _h.RefreshAsync();

        _h.Records().Select(r => (r.Url, r.State, r.RunId)).Should().BeEquivalentTo(
            [(Pr1, PrReviewStates.Closed, "run-1"), (Pr2, PrReviewStates.Open, "run-2")]);
    }

    [Fact]
    public async Task PrReviewRecordRefresh_LeaseExpiredMidRefresh_OlderReadNeverOverwritesNewer()
    {
        using var scope = _h.Store.Services.CreateScope();
        var writer = new PrReviewRecordWriter(_h.Store.Context(), new SqliteUniqueViolationTranslator());
        PrReviewRecord Read(string state, long checkedAt) =>
            new() { PrKey = PrKey.Of(Pr1), Url = Pr1, Project = "p", TicketId = "7", RunId = "run-1", State = state, CheckedAtTicks = checkedAt };
        await writer.WriteAsync(Read(PrReviewStates.Open, 10), [], readStartTicks: 5, CancellationToken.None);

        (await writer.WriteAsync(Read(PrReviewStates.Merged, 30), [], readStartTicks: 20, CancellationToken.None)).Should().BeTrue();
        (await new PrReviewRecordWriter(_h.Store.Context(), new SqliteUniqueViolationTranslator())
            .WriteAsync(Read(PrReviewStates.Open, 40), [], readStartTicks: 15, CancellationToken.None))
            .Should().BeFalse("a read that started before the newer write is older than it");

        _h.Records().Single().Should().Match<PrReviewRecord>(r => r.State == PrReviewStates.Merged && r.CheckedAtTicks == 30);
    }

    [Fact]
    public async Task PrReviewRecordRefresh_HostFailure_WritesUnreadOnce_KeepsTheStateAndAdvancesItsCheckTime()
    {
        await _h.RunAsync("run-1", Pr1);
        _h.Provider.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(Pr1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("host down"));

        await _h.RefreshAsync();
        _h.Records().Single().State.Should().Be(PrReviewStates.Unread);
        var checkedAt = _h.Records().Single().CheckedAtTicks;
        _h.Clock.Now += TimeSpan.FromMinutes(1);
        await _h.RefreshAsync();

        _h.Records().Should().ContainSingle().Which.Should().Match<PrReviewRecord>(r =>
            r.State == PrReviewStates.Unread && r.CheckedAtTicks == checkedAt + TimeSpan.FromMinutes(1).Ticks,
            "a failure keeps the record's state and moves it to the back of the re-read");
    }

    [Fact]
    public async Task PrReviewRecordWriter_FailedReadOfAnExistingRecord_AdvancesOnlyItsCheckTime()
    {
        var writer = new PrReviewRecordWriter(_h.Store.Context(), new SqliteUniqueViolationTranslator());
        PrReviewRecord Read(string state, long checkedAt) => new()
        {
            PrKey = PrKey.Of(Pr1), Url = Pr1, Project = "p", TicketId = "7", RunId = "run-1", State = state,
            StateAtTicks = state == PrReviewStates.Merged ? 7 : null, CheckedAtTicks = checkedAt,
        };
        var entry = new PrReviewEntry { PrKey = PrKey.Of(Pr1), HostId = "C1", Kind = PrReviewEntryKinds.Comment, Body = "fix", AtTicks = 3 };
        await writer.WriteAsync(Read(PrReviewStates.Merged, 10), [entry], readStartTicks: 5, CancellationToken.None);

        (await new PrReviewRecordWriter(_h.Store.Context(), new SqliteUniqueViolationTranslator())
            .MarkUnreadAsync(Read(PrReviewStates.Unread, 50), readStartTicks: 40, CancellationToken.None))
            .Should().BeFalse("nothing a reader shows changed");
        (await new PrReviewRecordWriter(_h.Store.Context(), new SqliteUniqueViolationTranslator())
            .MarkUnreadAsync(Read(PrReviewStates.Unread, 45), readStartTicks: 30, CancellationToken.None))
            .Should().BeFalse();

        _h.Records().Single().Should().Match<PrReviewRecord>(r =>
            r.State == PrReviewStates.Merged && r.StateAtTicks == 7 && r.CheckedAtTicks == 50, "an older read never moves it back");
        _h.Entries().Should().ContainSingle().Which.Body.Should().Be("fix");
    }

    [Fact]
    public async Task PrReviewRecordRefresh_LocalRepo_WritesUnsupported()
    {
        await _h.RunAsync("run-1", Pr1);
        _h.Provider = new Mock<ISourceProvider>();

        await _h.RefreshAsync();

        _h.Records().Single().State.Should().Be(PrReviewStates.Unsupported);
    }

    [Fact]
    public async Task PrReviewRecordRefresh_ChangedRecord_PublishesTicketActivity_UnchangedDoesNot()
    {
        await _h.RunAsync("run-1", Pr1);
        _h.State(Pr1, PrReviewStates.Open);
        _h.Threads([new PrReviewThread(null, null, null, [Note("C1", "alice", "hi")]) { HostId = "C1" }]);

        await _h.RefreshAsync();
        _h.Clock.Now += TimeSpan.FromMinutes(1);
        await _h.RefreshAsync();

        _h.Activity.Verify(a => a.ChangedAsync("p", "github-7", It.IsAny<CancellationToken>()), Times.Once);
    }
}
