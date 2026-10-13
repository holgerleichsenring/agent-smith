using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AgentSmith.Tests.Rework.Records.PrReviewRefreshHarness;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>
/// 2026-10-13-9a59b: a Refresh nudge refreshes the records and nothing else; the claim resets the
/// origin so a Refresh merged during handling never re-runs the handler.
/// </summary>
public sealed class ReworkNudgeRefreshTests : IDisposable
{
    private readonly PrReviewRefreshHarness _h = new();
    private readonly Mock<IPreviousAttemptReader> _attempts = new();
    private readonly Mock<IReworkPendingActs> _pending = new();
    private readonly Mock<IReworkEntry> _rework = new();

    public void Dispose() => _h.Dispose();

    private ReworkNudgeRepository Repo() => _h.Store.Services.CreateScope().ServiceProvider.GetRequiredService<ReworkNudgeRepository>();

    private List<ReworkNudge> Rows() => [.. _h.Store.Context().Set<ReworkNudge>()];

    private ReworkNudgeWorker Worker()
    {
        var tickets = new Mock<ITicketProviderFactory>();
        tickets.Setup(t => t.Create(It.IsAny<TrackerConnection>())).Returns(Mock.Of<ITicketProvider>());
        var speech = new ReworkSpeech(_h.Store.ScopeFactory, tickets.Object, Mock.Of<ISourceProviderFactory>(), NullLogger<ReworkSpeech>.Instance);
        var fallback = new ReworkFallbackSpawn(Mock.Of<IConfigurationLoader>(), new ServerContext("c.yml"), tickets.Object,
            new PolledTicketEnvelope(ApprovedRecordProbes.None()), Mock.Of<IEnvelopeProjectResolver>(),
            Mock.Of<ISpawnPipelineRunsUseCase>(), NullLogger<ReworkFallbackSpawn>.Instance);
        var handler = new ReworkNudgeHandler(Mock.Of<IActiveRunLease>(), _attempts.Object, _pending.Object, _rework.Object, speech,
            fallback, new ReworkWithheld(_h.Store.ScopeFactory, speech), Mock.Of<IRunCheckpointStore>());
        var locks = new Mock<IRedisClaimLock>();
        locks.Setup(l => l.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync("lock");
        var config = new Mock<IConfigurationLoader>();
        config.Setup(c => c.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig { Projects = new() { ["p"] = Project } });
        return new ReworkNudgeWorker(_h.Store.ScopeFactory, locks.Object, config.Object, new ServerContext("c.yml"), handler,
            _h.Refresh(), NullLogger<ReworkNudgeWorker>.Instance);
    }

    private void Attempt(string status, bool finished) =>
        _attempts.Setup(a => a.LatestAsync("p", "7", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("run-1", status, T, finished));

    private Task Enqueue(ReworkNudgeOrigin origin) =>
        Repo().EnqueueAsync(new ReworkNudgeRequest("p", "7", origin), CancellationToken.None);

    private void HostDown() =>
        _h.Provider.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("host down"));

    [Fact]
    public async Task RefreshNudge_HostFailure_FinishesWithoutRetryLoop()
    {
        await _h.RunAsync("run-1", Pr1);
        HostDown();
        await Enqueue(ReworkNudgeOrigin.Refresh);

        await Worker().DrainAsync(CancellationToken.None);

        Rows().Should().BeEmpty("the failure was written; the next real trigger reads again");
        _h.Records().Single().State.Should().Be(PrReviewStates.Unread);
    }

    [Fact]
    public async Task PrReviewRecordRefresh_HostFailure_WritesUnread_HandlerDispositionStands()
    {
        await _h.RunAsync("run-1", Pr1);
        HostDown();
        Attempt("running", finished: false);
        await Enqueue(ReworkNudgeOrigin.Ticket);

        await Worker().DrainAsync(CancellationToken.None);

        _h.Records().Single().State.Should().Be(PrReviewStates.Unread);
        Rows().Single().Should().Match<ReworkNudge>(n => n.Tries == 1 && n.Origin == (int)ReworkNudgeOrigin.Ticket,
            "the handler's reschedule stands and keeps the person's origin");
    }

    [Fact]
    public async Task RefreshNudge_StandingRefusedAct_IsNotReEntered()
    {
        await _h.RunAsync("run-1", Pr1);
        _h.State(Pr1, PrReviewStates.Open);
        Attempt("success", finished: true);
        _pending.Setup(p => p.NewestAsync(It.IsAny<ResolvedProject>(), "7", It.IsAny<PreviousAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PendingReworkAct(new ReworkAct("alice", T.AddMinutes(30))));
        await Enqueue(ReworkNudgeOrigin.Refresh);

        await Worker().DrainAsync(CancellationToken.None);

        _h.Records().Should().ContainSingle();
        _attempts.VerifyNoOtherCalls();
        _pending.VerifyNoOtherCalls();
        _rework.VerifyNoOtherCalls();
        Rows().Should().BeEmpty();
    }

    [Fact]
    public async Task ReworkNudgeWriter_RefreshMergedWithPullRequest_KeepsPullRequestOrigin()
    {
        await Enqueue(ReworkNudgeOrigin.PullRequest);
        await Enqueue(ReworkNudgeOrigin.Refresh);
        await Enqueue(ReworkNudgeOrigin.Refresh);

        Rows().Single().Origin.Should().Be((int)ReworkNudgeOrigin.PullRequest);
    }

    [Fact]
    public async Task RefreshMergedDuringClaim_DoesNotRunTheHandlerAgain()
    {
        await Enqueue(ReworkNudgeOrigin.Ticket);
        var claimed = (await Repo().ClaimDueAsync(10, CancellationToken.None)).Single();
        claimed.Origin.Should().Be(ReworkNudgeOrigin.Ticket);
        await Enqueue(ReworkNudgeOrigin.Refresh);

        await Repo().FinishAsync(claimed, CancellationToken.None);

        (await Repo().ClaimDueAsync(10, CancellationToken.None)).Single().Origin.Should().Be(ReworkNudgeOrigin.Refresh,
            "only the Refresh merged after the claim is left to serve");
    }

    [Fact]
    public async Task RescheduleAfterMerge_KeepsTheClaimedOrigin()
    {
        await Enqueue(ReworkNudgeOrigin.Ticket);
        var claimed = (await Repo().ClaimDueAsync(10, CancellationToken.None)).Single();
        await Enqueue(ReworkNudgeOrigin.Refresh);

        await Repo().RescheduleAsync(claimed, TimeSpan.FromSeconds(15), CancellationToken.None);

        Rows().Single().Origin.Should().Be((int)ReworkNudgeOrigin.Ticket, "the person's act must still be served");
    }

    [Fact]
    public async Task CrashedClaim_ReclaimedAfterExpiry_KeepsTheClaimedOrigin()
    {
        await Enqueue(ReworkNudgeOrigin.Ticket);
        (await Repo().ClaimDueAsync(10, CancellationToken.None)).Should().ContainSingle();
        _h.Clock.Now += ReworkNudgeRepository.ClaimLease + TimeSpan.FromSeconds(1);

        (await Repo().ClaimDueAsync(10, CancellationToken.None)).Single().Origin.Should().Be(ReworkNudgeOrigin.Ticket,
            "a worker that died mid-claim must not turn a person's act into a refresh");
    }

    [Fact]
    public void ReworkNudgeRequest_DeliveryRefresh_IsNotLate_SweepRefreshIs()
    {
        new ReworkNudgeRequest("p", "7", ReworkNudgeOrigin.Refresh) { FromDelivery = true }.IsLate.Should().BeFalse();
        new ReworkNudgeRequest("p", "7", ReworkNudgeOrigin.Refresh).IsLate.Should().BeTrue();
        new ReworkNudgeRequest("p", "7", ReworkNudgeOrigin.Sweep).IsLate.Should().BeTrue();
    }

    [Fact]
    public async Task RunDeletion_NewestRunNamingPr_DeletesRecordAndEntries()
    {
        await _h.RunAsync("run-1", Pr1);
        await using (var ctx = _h.Store.Context())
        {
            ctx.AddRange(
                new PrReviewRecord { PrKey = PrKey.Of(Pr1), Url = Pr1, Project = "p", TicketId = "7", RunId = "run-1", State = "open" },
                new PrReviewEntry { PrKey = PrKey.Of(Pr1), HostId = "C1", Kind = "comment" },
                new PrReviewRecord { PrKey = PrKey.Of(Pr2), Url = Pr2, Project = "p", TicketId = "7", RunId = "run-9", State = "open" },
                new PrReviewEntry { PrKey = PrKey.Of(Pr2), HostId = "C2", Kind = "comment" });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _h.Store.Context())
            await new RunDeletionRepository(ctx, new SqliteUniqueViolationTranslator()).DeleteAsync("run-1", CancellationToken.None);

        _h.Records().Select(r => r.Url).Should().Equal(Pr2);
        _h.Entries().Select(e => e.HostId).Should().Equal("C2");
    }

    [Fact]
    public async Task RefreshDuringHandledPullRequestNudge_HandlerRunsOnce()
    {
        await _h.RunAsync("run-1", Pr1);
        _h.State(Pr1, PrReviewStates.Open);
        var handled = 0;
        _attempts.Setup(a => a.LatestAsync("p", "7", null, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                // A merge delivery arrives while the act is being handled.
                if (handled++ == 0)
                    await Repo().EnqueueAsync(new ReworkNudgeRequest("p", "7", ReworkNudgeOrigin.Refresh, Pr1) { FromDelivery = true }, CancellationToken.None);
                return new PreviousAttempt("run-1", "success", T, true);
            });
        await Enqueue(ReworkNudgeOrigin.PullRequest);

        await Worker().DrainAsync(CancellationToken.None);
        var afterAct = handled;
        await Worker().DrainAsync(CancellationToken.None);

        afterAct.Should().BeGreaterThan(0);
        handled.Should().Be(afterAct, "the refresh merged during handling only refreshes the record");
        Rows().Should().BeEmpty();
    }

    [Fact]
    public async Task PrMergedDelivery_TicketNudgeInBackoff_RefreshesWithinOneDrain()
    {
        await _h.RunAsync("run-1", Pr1);
        Attempt("success", finished: true);
        await Enqueue(ReworkNudgeOrigin.Ticket);
        var claimed = (await Repo().ClaimDueAsync(10, CancellationToken.None)).Single();
        await Repo().RescheduleAsync(claimed, TimeSpan.FromMinutes(15), CancellationToken.None);
        _h.State(Pr1, PrReviewStates.Merged);

        await Repo().EnqueueAsync(new ReworkNudgeRequest("p", "7", ReworkNudgeOrigin.Refresh, Pr1) { FromDelivery = true }, CancellationToken.None);
        await Worker().DrainAsync(CancellationToken.None);

        _h.Records().Single().State.Should().Be(PrReviewStates.Merged, "a delivery's refresh is not held behind the ticket's backoff");
    }
}
