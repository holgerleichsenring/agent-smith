using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Application.Services.Rework;
using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Lifecycle;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.Webhooks;
using AgentSmith.Tests.SpecDialog.Continuation;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using AgentSmith.Tests.Tickets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework.FollowUp;

/// <summary>
/// 2026-10-13-9a59t: an act on a ticket a person closed files one linked follow-up instead of reopening it —
/// over one migrated store, the durable nudge queue, the real worker reads, the REAL entry, park check and
/// closure, and the real follow-up with its scoped filing. Ticket 42 of alpha (and beta, on the same
/// tracker) is closed in the tracker's own status "Closed"; done_status is "In Review".
/// </summary>
public sealed class ClosedTicketFollowUpTests : IDisposable
{
    private const string Keyword = "@agent-smith";
    private const string PrUrl = "https://github.com/o/api/pull/4";
    private static readonly DateTimeOffset Later = TicketStoreHarness.T.AddHours(2);

    private readonly ContinuationHarness _h = new();
    private readonly Mock<IReworkLaunch> _launch = new();
    private readonly Mock<IWaitingRunCloser> _closer = new();
    private readonly Mock<ISourceProvider> _source = new();
    private readonly Mock<ISourceProviderFactory> _sources = new();
    private readonly ClosedTicketFollowUps.InMemoryLock _locks = new();

    public ClosedTicketFollowUpTests()
    {
        foreach (var project in _h.Config.Projects.Values) project.GithubTrigger!.CommentKeyword = Keyword;
        _h.Tracker.Status = "Closed";
        _h.Tracker.IsClosed = true;
        _launch.Setup(l => l.LaunchAsync(It.IsAny<string>(), "42", "code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReworkOutcome.Started("r-2"));
        _source.As<IPrCommentProvider>();
        _sources.Setup(s => s.Create(It.IsAny<RepoConnection>())).Returns(_source.Object);
    }

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task ClosedTicket_KeywordComment_FilesFollowUpWithCommentText_DoesNotReopen()
    {
        await AttemptAsync("r-1", RunStatuses.Failed);
        Comment("@agent-smith the export also needs a CSV column");

        (await HandleAsync()).Should().Be(ReworkNudgeDisposition.Finish);

        _h.Tracker.Titles.Should().ContainSingle().Which.Should().Be("Follow-up: Widget drops");
        _h.Tracker.Created.Single().Should().Contain("the export also needs a CSV column").And.Contain("#42").And.Contain("carol");
        _h.Tracker.Moves.Should().BeEmpty("the closed ticket stays closed");
        _launch.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Harness_KeywordCommentOnPersonClosedTicket_FollowUpFiledAndLinked()
    {
        await AttemptAsync("r-1", RunStatuses.Success);
        Comment("@agent-smith also cover the archive view");

        await RouteAsync(new ProjectMatch("alpha", "code", "github"));
        (await HandleAllAsync()).Should().Equal(ReworkNudgeDisposition.Finish);

        _h.Tracker.Created.Should().ContainSingle().Which.Should().Contain("also cover the archive view");
        _h.Tracker.Comments.Should().Contain(c => c.TicketId == "42" && c.Comment.Contains("#43") && c.Comment.Contains("carol"));
        _h.Tracker.Comments.Should().Contain(c => c.TicketId == "43" && c.Comment.Contains("follows up #42"));
        _h.Tracker.Status.Should().Be("Closed");
    }

    [Fact]
    public async Task ClosedTicket_KeywordComment_TwoMatchingProjects_FilesOnce()
    {
        await AttemptAsync("r-1", RunStatuses.Failed);
        await AttemptAsync("r-b", RunStatuses.Failed, "beta");
        Comment("@agent-smith one more field");

        await RouteAsync(new ProjectMatch("alpha", "code", "github"), new ProjectMatch("beta", "code", "github"));
        (await HandleAllAsync()).Should().HaveCount(2);

        _h.Tracker.Created.Should().ContainSingle("one tracker ticket gets one follow-up whichever project matched");
    }

    [Fact]
    public async Task ClosedTicket_SameActTwice_FilesOnce()
    {
        await AttemptAsync("r-1", RunStatuses.Failed);
        Comment("@agent-smith one more field");

        await HandleAsync();
        await HandleAsync();

        _h.Tracker.Created.Should().ContainSingle();
    }

    [Fact]
    public async Task ClosedTicket_CreateFails_KeySpent_NoDuplicate()
    {
        await AttemptAsync("r-1", RunStatuses.Failed);
        Comment("@agent-smith one more field");
        _h.Tracker.CreateFails = new HttpRequestException("tracker down");

        (await HandleAsync()).Should().Be(ReworkNudgeDisposition.Finish, "a failed create is logged, never retried");
        _h.Tracker.CreateFails = null;
        await HandleAsync();

        _h.Tracker.Created.Should().BeEmpty("the key was marked before the create, so nothing files twice");
    }

    [Fact]
    public async Task ClosedTicket_FollowedUpAct_TicketReopenedLater_NeverReworks()
    {
        await AttemptAsync("r-1", RunStatuses.Failed);
        Comment("@agent-smith one more field");
        await HandleAsync();
        _h.Tracker.Status = "Open";
        _h.Tracker.IsClosed = false;

        (await HandleAsync()).Should().Be(ReworkNudgeDisposition.Finish);

        _launch.VerifyNoOtherCalls();
        _h.Tracker.Created.Should().ContainSingle();
    }

    [Fact]
    public async Task NudgeHandler_ClosedTicket_CheckpointedQuestionWaiting_ClosedThenFollowUpFiled()
    {
        await _h.AddAsync(TicketStoreHarness.Run("r-1", status: RunStatuses.WaitingForInput), TicketStoreHarness.Step("r-1"),
            TicketStoreHarness.Checkpoint("r-1"));
        Comment("@agent-smith forget the question, add the CSV column");

        (await HandleAsync()).Should().Be(ReworkNudgeDisposition.Finish, "nobody answers a question on a closed ticket");

        _closer.Verify(c => c.CloseSupersededAsync("r-1", It.IsAny<CancellationToken>()), Times.Once);
        _h.Tracker.Created.Should().ContainSingle().Which.Should().Contain("add the CSV column");
    }

    [Fact]
    public async Task ClosedTicket_RequestChanges_FilesFollowUp_AnswersOnPullRequest()
    {
        await AttemptAsync("r-1", RunStatuses.Success);
        var act = new PendingReworkAct(new ReworkAct("dana", Later, ReworkChannel.PullRequest),
            new RepoConnection { Name = "api", Type = RepoType.GitHub }, PrUrl, "rename the flag too");

        (await HandleAsync(Pending(act))).Should().Be(ReworkNudgeDisposition.Finish);

        _h.Tracker.Created.Should().ContainSingle().Which.Should().Contain("rename the flag too");
        _source.As<IPrCommentProvider>().Verify(p => p.PostCommentAsync("4", It.Is<string>(t => t.Contains("continues on #43")), It.IsAny<CancellationToken>()),
            Times.Once);
        _launch.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OwnDoneTicket_RequestChangesOnOwnPr_StillReworks()
    {
        await AttemptAsync("r-1", RunStatuses.Success);
        _h.Tracker.Status = "In Review"; // agent-smith's own done_status, counted closed by the tracker
        var act = new PendingReworkAct(new ReworkAct("dana", Later, ReworkChannel.PullRequest),
            new RepoConnection { Name = "api", Type = RepoType.GitHub }, PrUrl, "rename the flag too");

        (await HandleAsync(Pending(act))).Should().Be(ReworkNudgeDisposition.Finish);

        _launch.Verify(l => l.LaunchAsync("alpha", "42", "code", It.IsAny<CancellationToken>()), Times.Once);
        _h.Tracker.Created.Should().BeEmpty();
    }

    // 2026-10-13-8762p: an overruled default on a ticket a person closed is followed up like a keyword comment.
    [Fact]
    public async Task ClosedTicket_Overrule_FiledFollowUpCarriesTheNote()
    {
        await AttemptAsync("r-1", RunStatuses.Success);
        var attempt = new PreviousAttempt("r-1", RunStatuses.Success, TicketStoreHarness.T, true)
        {
            Overrules = [new(7, "r-1", null, "Which queue?", "polling", "overruled", "use the bus", "ana", Later)],
        };
        var act = new PendingReworkAct(Application.Services.Rework.OverruleAct.Of(attempt)!, Text: Application.Services.Rework.OverruleAct.TextOf(attempt));

        (await HandleAsync(Pending(act))).Should().Be(ReworkNudgeDisposition.Finish);

        _h.Tracker.Created.Should().ContainSingle().Which.Should().Contain("Which queue?").And.Contain("use the bus").And.Contain("ana");
        _h.Tracker.Status.Should().Be("Closed");
        _launch.VerifyNoOtherCalls();
    }

    [Fact]
    public Task ClosedTicket_OperatorAct_RefusedNamingConversation() => RefusedNamingConversationAsync(ReworkChannel.Operator);

    [Fact]
    public Task ClosedTicket_RetryAct_RefusedNamingConversation() => RefusedNamingConversationAsync(ReworkChannel.Retry);

    private async Task RefusedNamingConversationAsync(ReworkChannel channel)
    {
        await AttemptAsync("r-1", RunStatuses.Failed);

        await HandleAsync(Pending(new PendingReworkAct(new ReworkAct("owner", Later, channel))));

        _h.Tracker.Created.Should().BeEmpty();
        _h.Tracker.Comments.Should().ContainSingle(c => c.TicketId == "42" && c.Comment.StartsWith("Agent Smith —")
            && c.Comment.Contains("approve a change in its conversation to file a follow-up"));
    }

    private void Comment(string body) =>
        _h.Tracker.Thread.Add(new TicketComment("carol", Later, body) { Id = "c-9" });

    private Task AttemptAsync(string runId, string status, string project = "alpha") =>
        _h.AddAsync(TicketStoreHarness.Run(runId, status: status, project: project), TicketStoreHarness.Step(runId));

    private static IReworkPendingActs Pending(PendingReworkAct act)
    {
        var pending = new Mock<IReworkPendingActs>();
        pending.Setup(p => p.NewestAsync(It.IsAny<ResolvedProject>(), "42", It.IsAny<PreviousAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(act);
        return pending.Object;
    }

    private async Task RouteAsync(params ProjectMatch[] matches)
    {
        var dispatcher = new WebhookSpawnDispatcher(Mock.Of<ISpawnPipelineRunsUseCase>(), _h.Tracker,
            NullLogger<WebhookSpawnDispatcher>.Instance);
        var router = new KeywordCommentRouter(dispatcher, _h.Services.GetRequiredService<IPreviousAttemptReader>(),
            _h.Services.GetRequiredService<IReworkNudges>(), NullLogger<KeywordCommentRouter>.Instance);
        await router.RouteAsync(_h.Config, matches, new KeywordComment(
            new IncomingTicketEnvelope { TicketId = "42", Platform = "github" }, "Closed", _h.Tracker.Thread[^1].Body, null,
            new ReworkAct("carol", Later)), default);
    }

    // One nudge enqueued as a keyword comment does, then one worker pass over it.
    private async Task<ReworkNudgeDisposition> HandleAsync(IReworkPendingActs? pending = null)
    {
        await _h.Services.GetRequiredService<IReworkNudges>().EnqueueAsync(
            new ReworkNudgeRequest("alpha", "42", ReworkNudgeOrigin.Ticket, Channel: ReworkChannel.Ticket), default);
        return (await HandleAllAsync(pending)).Single();
    }

    // What ReworkNudgeWorker does with every claimed row: handle under its project, finish.
    private async Task<List<ReworkNudgeDisposition>> HandleAllAsync(IReworkPendingActs? pending = null)
    {
        var claimed = await _h.InScopeAsync(sp => sp.GetRequiredService<ReworkNudgeRepository>().ClaimDueAsync(10, default));
        var dispositions = new List<ReworkNudgeDisposition>();
        foreach (var nudge in claimed)
        {
            dispositions.Add(await Handler(pending).HandleAsync(_h.Config.Projects[nudge.Project], nudge, default));
            await _h.InScopeAsync(async sp =>
            {
                await sp.GetRequiredService<ReworkNudgeRepository>().FinishAsync(nudge, default);
                return true;
            });
        }
        return dispositions;
    }

    private ReworkNudgeHandler Handler(IReworkPendingActs? pending)
    {
        var sp = _h.Services;
        var scopes = sp.GetRequiredService<IServiceScopeFactory>();
        var leases = sp.GetRequiredService<IActiveRunLease>();
        var attempts = sp.GetRequiredService<IPreviousAttemptReader>();
        var speech = new ReworkSpeech(scopes, _h.Tracker, _sources.Object, NullLogger<ReworkSpeech>.Instance);
        var fallback = new ReworkFallbackSpawn(Mock.Of<IConfigurationLoader>(), new ServerContext("c.yml"), _h.Tracker,
            new PolledTicketEnvelope(ApprovedRecordProbes.None()), Mock.Of<IEnvelopeProjectResolver>(),
            Mock.Of<ISpawnPipelineRunsUseCase>(), NullLogger<ReworkFallbackSpawn>.Instance);
        var parks = new ReworkParkCheck(_h.Tracker, Mock.Of<IUnmovedTicketStore>(), sp.GetRequiredService<TicketClosure>());
        var entry = new ReworkEntry(leases, attempts, parks,
            new TicketReopener(new InMemorySpecSetPointerStore(), _h.Approvals, Mock.Of<IUnmovedTicketStore>()),
            _launch.Object, _closer.Object, NullLogger<ReworkEntry>.Instance);
        pending ??= new ReworkPendingActs(_h.Tracker, _sources.Object, Mock.Of<IPrReviewAuthorTrust>(), _h.Approvals);
        return ReworkHandlers.Of(leases, attempts, pending, entry, speech, fallback, new ReworkWithheld(scopes, speech),
            sp.GetRequiredService<IRunCheckpointStore>(), ClosedTicketFollowUps.Of(scopes, _h.Tracker, speech, _locks));
    }
}
