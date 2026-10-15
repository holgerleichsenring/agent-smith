using System.Text.Json;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using AgentSmith.Tests.Tickets.NeverRun;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using static AgentSmith.Tests.Tickets.Closing.TicketCloseFixture;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.Closing;

/// <summary>
/// 2026-10-15-114fa: a person closes a ticket — the mark records who, when and why; pull requests and the tracker
/// ticket close only when chosen, every named pull request is answered and a refusing host is named by class. It
/// is refused exactly where the rule says Done, while an attempt is live, and when a merged pull request would make
/// closing the rest a landing.
/// </summary>
public sealed class TicketCloseActTests : IDisposable
{
    private readonly TicketCloseFixture _f = new();

    public void Dispose() => _f.Dispose();

    private static TicketCloseAnswer Answer(TicketCloseResult result)
    {
        result.Status.Should().Be(StatusCodes.Status200OK, result.Refusal);
        return (TicketCloseAnswer)result.Body!;
    }

    private void NothingClosed()
    {
        _f.Closer.Verify(c => c.CloseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _f.H.Tracker.Verify(t => t.CloseTicketAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TicketClose_HideOnly_StoresTheMarkAndTouchesNoHost()
    {
        await _f.H.AddAsync(Named("r-1", "failed", ("api", "7")), Step("r-1"), Record("7", PrReviewStates.Open));

        var answer = Answer(await _f.CloseAsync());

        answer.PullRequests.Should().BeEmpty();
        answer.Tracker.Should().BeNull();
        (await _f.MarkAsync()).Should().BeEquivalentTo(answer.Mark);
        answer.Mark.Should().Match<TicketClosedMark>(m => m.ClosedPullRequests.Count == 0 && !m.TrackerClosed);
        NothingClosed();
        _f.H.Sources.Verify(s => s.Create(It.IsAny<AgentSmith.Contracts.Models.Configuration.RepoConnection>()), Times.Never);
    }

    [Fact]
    public async Task TicketClose_WithANote_StoresWhoWhenAndTheTrimmedNote()
    {
        await _f.H.AddAsync(NeverRunFixtures.Filing("42"));

        Answer(await _f.CloseAsync(note: "  superseded by 43  "));
        var mark = await _f.MarkAsync();

        mark.Should().BeEquivalentTo(new TicketClosedMark(Tracker, Key("42"), "42", "alpha", T.AddHours(3), "Ada", "superseded by 43", [], false));
        Answer(await _f.CloseAsync(note: "   "));
        (await _f.MarkAsync())!.Note.Should().BeNull("a blank note is no note");
    }

    [Fact]
    public async Task TicketClose_ClosePullRequests_ClosesEveryOpenRecordAndListsThemOnTheMark()
    {
        await _f.H.AddAsync(Named("r-1", "failed", ("api", "7")), Named("r-2", "failed", ("api", "8")), Step("r-1"), Step("r-2"),
            Record("7", PrReviewStates.Open), Record("8", PrReviewStates.Open));

        var answer = Answer(await _f.CloseAsync(prs: true));

        answer.PullRequests.Should().BeEquivalentTo([new TicketPrCloseOutcome(Url("7"), true, null), new TicketPrCloseOutcome(Url("8"), true, null)]);
        (await _f.MarkAsync())!.ClosedPullRequests.Should().BeEquivalentTo([Url("7"), Url("8")]);
        _f.Closer.Verify(c => c.CloseAsync(Url("7"), CancellationToken.None), Times.Once);
        _f.Closer.Verify(c => c.CloseAsync(Url("8"), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task TicketClose_ClosePullRequests_TriesAnUnreadRecordAndANamedPullRequestWithoutARecord()
    {
        await _f.H.AddAsync(Named("r-1", "failed", ("api", "7"), ("api", "8")), Step("r-1"), Record("7", PrReviewStates.Unread));

        var answer = Answer(await _f.CloseAsync(prs: true));

        answer.PullRequests.Should().OnlyContain(p => p.Closed && p.Reason == null).And.HaveCount(2);
        _f.Closer.Verify(c => c.CloseAsync(Url("8"), It.IsAny<CancellationToken>()), Times.Once, "the refresh has not recorded it yet");
    }

    [Fact]
    public async Task TicketClose_ClosePullRequests_AnswersAnAlreadyClosedOneWithoutTryingIt()
    {
        await _f.H.AddAsync(Named("r-1", "failed", ("api", "7"), ("api", "8")), Step("r-1"),
            Record("7", PrReviewStates.Closed), Record("8", PrReviewStates.Open));

        var answer = Answer(await _f.CloseAsync(prs: true));

        answer.PullRequests.Should().ContainEquivalentOf(new TicketPrCloseOutcome(Url("7"), true, TicketCloseReasons.AlreadyClosed));
        _f.Closer.Verify(c => c.CloseAsync(Url("7"), It.IsAny<CancellationToken>()), Times.Never);
        (await _f.MarkAsync())!.ClosedPullRequests.Should().Equal(Url("8"));
    }

    [Fact]
    public async Task TicketClose_ClosePullRequestsWithOneMerged_Is409AndClosesNothing()
    {
        await _f.H.AddAsync(Named("r-1", "failed", ("api", "7"), ("api", "8")), Step("r-1"),
            Record("7", PrReviewStates.Merged), Record("8", PrReviewStates.Open));

        var result = await _f.CloseAsync(prs: true);

        result.Status.Should().Be(StatusCodes.Status409Conflict);
        result.Refusal.Should().Be("one of 42's pull requests merged; closing the rest would make it land — "
            + "mark it done by hand, or close without closing its pull requests.");
        NothingClosed();
        (await _f.MarkAsync()).Should().BeNull();
    }

    [Fact]
    public async Task TicketClose_OnePullRequestRefused_StoresTheMarkWithTheOtherAndNamesTheClass()
    {
        await _f.H.AddAsync(Named("r-1", "failed", ("api", "7"), ("api", "8")), Step("r-1"));
        _f.Closer.Setup(c => c.CloseAsync(Url("7"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Octokit.ApiException("Validation Failed at secret-host.internal", (System.Net.HttpStatusCode)422));

        var answer = Answer(await _f.CloseAsync(prs: true));

        answer.PullRequests.Should().BeEquivalentTo([
            new TicketPrCloseOutcome(Url("7"), false, TicketCloseReasons.Refused), new TicketPrCloseOutcome(Url("8"), true, null)]);
        (await _f.MarkAsync())!.ClosedPullRequests.Should().Equal(Url("8"));
        JsonSerializer.Serialize(answer).Should().NotContain("secret-host", "only the class leaves the server");
    }

    [Fact]
    public async Task TicketClose_PullRequestOfALocalRepository_IsUnsupported()
    {
        await _f.H.AddAsync(Named("r-1", "failed", ("local", "3")), Step("r-1"), Record("3", PrReviewStates.Unsupported, repo: "local"));

        var answer = Answer(await _f.CloseAsync(prs: true));

        answer.PullRequests.Should().Equal(new TicketPrCloseOutcome(Url("3", "local"), false, TicketCloseReasons.Unsupported));
        (await _f.MarkAsync())!.ClosedPullRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task TicketClose_CloseOnTracker_CallsCloseTicketWithTheMarkedResolution()
    {
        await _f.H.AddAsync(Run("r-1", "42", "failed"), Step("r-1"));

        var answer = Answer(await _f.CloseAsync(note: "done elsewhere", tracker: true));

        _f.H.Tracker.Verify(t => t.CloseTicketAsync(new TicketId("42"), "Agent Smith — closed by Ada: done elsewhere", CancellationToken.None), Times.Once);
        answer.Tracker.Should().Be(new TicketTrackerCloseOutcome(true, null));
        (await _f.MarkAsync())!.TrackerClosed.Should().BeTrue();
    }

    [Fact]
    public async Task TicketClose_ResolutionWithTheCommentKeyword_IsAgentSmithsOwnComment()
    {
        await _f.H.AddAsync(Run("r-1", "42", "failed"), Step("r-1"));
        string? resolution = null;
        _f.H.Tracker.Setup(t => t.CloseTicketAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<TicketId, string, CancellationToken>((_, text, _) => resolution = text).ReturnsAsync(true);

        Answer(await _f.CloseAsync(note: "@agent-smith rework is not wanted", tracker: true));

        OwnTicketComment.IsOurs(resolution).Should().BeTrue("a keyword in the note must never start a rework");
    }

    [Fact]
    public async Task TicketClose_TrackerAnswersFalse_IsRefusedAndTheMarkSaysNotClosed()
    {
        await _f.H.AddAsync(Run("r-1", "42", "failed"), Step("r-1"));
        _f.H.Tracker.Setup(t => t.CloseTicketAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var answer = Answer(await _f.CloseAsync(tracker: true));

        answer.Tracker.Should().Be(new TicketTrackerCloseOutcome(false, TicketCloseReasons.Refused));
        (await _f.MarkAsync())!.TrackerClosed.Should().BeFalse();
    }

    [Fact]
    public async Task TicketClose_MergedAndNoLaterAttempt_Is409AndStoresNothing()
    {
        await _f.H.AddAsync(Named("r-1", "success", ("api", "7")), Step("r-1"), Record("7", PrReviewStates.Merged, T.AddHours(1)));
        await _f.H.LandAsync("42", TicketLandingKinds.Merged, T.AddHours(1));

        var result = await _f.CloseAsync();

        result.Status.Should().Be(StatusCodes.Status409Conflict);
        result.Refusal.Should().Be("42 already counts as landed (merged on 2026-10-13 10:00:00Z); a landed ticket is done, not closed.");
        (await _f.MarkAsync()).Should().BeNull();
    }

    [Fact]
    public async Task TicketClose_MarkedDoneByHand_Is409WithTheByHandWords()
    {
        await _f.H.AddAsync(NeverRunFixtures.Filing("42"));
        await _f.H.LandAsync("42");

        (await _f.CloseAsync()).Refusal.Should().Be(
            "42 already counts as landed (marked done by hand on 2026-10-13 11:00:00Z); a landed ticket is done, not closed.");
    }

    [Fact]
    public async Task TicketClose_FailedAttemptAfterAMerge_StoresTheMark()
    {
        await _f.H.AddAsync(Named("r-1", "failed", ("api", "7")), Step("r-1"), Record("7", PrReviewStates.Merged, T.AddHours(-1)));
        await _f.H.LandAsync("42", TicketLandingKinds.Merged, T.AddHours(-1));

        Answer(await _f.CloseAsync());

        (await _f.MarkAsync()).Should().NotBeNull("an attempt started after the merge is that attempt's ticket");
    }

    [Fact]
    public async Task TicketClose_RecordsMergedBeforeTheLandingRow_Is409()
    {
        await _f.H.AddAsync(Named("r-1", "success", ("api", "7")), Step("r-1"), Record("7", PrReviewStates.Merged, T.AddHours(1)));

        var result = await _f.CloseAsync();

        result.Status.Should().Be(StatusCodes.Status409Conflict, "the Done arm reads the records before the landing row exists");
        (await _f.MarkAsync()).Should().BeNull();
    }

    [Theory]
    [InlineData("running")]
    [InlineData(RunStatuses.Queued)]
    [InlineData(RunStatuses.WaitingForInput)]
    public async Task TicketClose_AttemptRunning_Is409AndStoresNothing(string status)
    {
        await _f.H.AddAsync(Run("r-1", "42", status), Step("r-1"));

        var result = await _f.CloseAsync(prs: true, tracker: true);

        result.Status.Should().Be(StatusCodes.Status409Conflict);
        result.Refusal.Should().Be("42 has an attempt running or waiting for an answer; stop it from the run controls first.");
        NothingClosed();
        (await _f.MarkAsync()).Should().BeNull();
    }

    [Fact]
    public async Task TicketClose_UnknownProject_Is404WithTheSentence()
    {
        var result = await _f.CloseAsync(project: "nowhere");

        (result.Status, result.Refusal).Should().Be((StatusCodes.Status404NotFound, "No project nowhere is configured."));
    }

    [Fact]
    public async Task TicketClose_TicketNeitherRunNorFiled_Is404WithTheSentence()
    {
        var result = await _f.CloseAsync(ticket: "99");

        (result.Status, result.Refusal).Should().Be((StatusCodes.Status404NotFound, "No ticket 99 is listed in alpha."));
        (await _f.MarkAsync("99")).Should().BeNull();
    }

    [Fact]
    public async Task TicketClose_SecondClose_OverwritesTheMark()
    {
        await _f.H.AddAsync(Run("r-1", "42", "failed"), Step("r-1"));

        Answer(await _f.CloseAsync(note: "first"));
        Answer(await _f.CloseAsync(note: "second", tracker: true));

        (await _f.MarkAsync()).Should().Match<TicketClosedMark>(m => m.Note == "second" && m.TrackerClosed);
    }

    [Fact]
    public async Task TicketClose_RequestCancelledAfterValidation_StillWritesTheMark()
    {
        await _f.H.AddAsync(Named("r-1", "failed", ("api", "7")), Step("r-1"), Record("7", PrReviewStates.Open));
        using var request = new CancellationTokenSource();
        _f.Closer.Setup(c => c.CloseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Callback(request.Cancel).Returns(Task.CompletedTask);

        Answer(await _f.CloseAsync(prs: true, ct: request.Token));

        request.IsCancellationRequested.Should().BeTrue();
        (await _f.MarkAsync())!.ClosedPullRequests.Should().Equal(Url("7"));
    }

    [Fact]
    public async Task TicketClose_PullRequestClosed_EnqueuesOneRefreshAndNudgesThePages()
    {
        await _f.H.AddAsync(Named("r-1", "failed", ("api", "7")), Step("r-1"), Record("7", PrReviewStates.Open));

        Answer(await _f.CloseAsync(prs: true));

        _f.H.Nudges.Verify(n => n.EnqueueAsync(It.Is<ReworkNudgeRequest>(r =>
            r.Project == "alpha" && r.TicketId == "42" && r.Origin == ReworkNudgeOrigin.Refresh), It.IsAny<CancellationToken>()), Times.Once);
        _f.H.Activity.Verify(a => a.ChangedAsync("alpha", Key("42"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TicketReopen_Closed_ClearsTheMarkAndNudgesThePages()
    {
        await _f.H.AddAsync(Run("r-1", "42", "failed"), Step("r-1"));
        Answer(await _f.CloseAsync());
        _f.H.Activity.Invocations.Clear();

        var result = await _f.Act.ReopenAsync("alpha", "42", default);

        result.Body.Should().Be(new TicketReopenAnswer("alpha", "42", Key("42")));
        (await _f.MarkAsync()).Should().BeNull();
        _f.H.Activity.Verify(a => a.ChangedAsync("alpha", Key("42"), It.IsAny<CancellationToken>()), Times.Once);
        NothingClosed();
    }

    [Fact]
    public async Task TicketReopen_NotClosed_Is404WithTheSentence()
    {
        var result = await _f.Act.ReopenAsync("alpha", "42", default);

        (result.Status, result.Refusal).Should().Be((StatusCodes.Status404NotFound, "42 is not closed."));
    }
}
