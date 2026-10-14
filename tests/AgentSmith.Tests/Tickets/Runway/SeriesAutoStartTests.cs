using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Rework;
using AgentSmith.Application.Services.Spawning;
using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.ChatLaunch;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.Tickets.Runway;
using AgentSmith.Tests.Spawning;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AgentSmith.Tests.Tickets.TicketSeriesFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.Runway;

/// <summary>2026-10-13-8762v: the worker's series start over the real runway reader and store — it starts, holds and
/// says why once, leaves a ticket a person closed alone, and every mark refreshes the page.</summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class SeriesAutoStartTests : IDisposable
{
    private const string R1 = "2026-10-14T09-00-00-0001";
    private const string A = "2026-10-14-5678a";
    private const string B = "2026-10-14-5678b";
    private const string C = "2026-10-14-5678c";
    private readonly Mock<IReworkLaunch> _launch = new();
    private readonly TicketStoreHarness _store;
    private ClaimRequest? _claimed;

    private static readonly ResolvedProject Alpha = new()
    {
        Name = "alpha", Repos = [new RepoConnection { Name = "api" }],
        Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
        GithubTrigger = new WebhookTriggerConfig { TriggerStatuses = ["Todo"], DoneStatus = "Shipped" },
    };

    public SeriesAutoStartTests()
    {
        _store = new TicketStoreHarness(services => services.AddScoped(_ => Launcher()));
        Ticket("Waiting");
        _launch.Setup(l => l.StartSeriesAttemptAsync("alpha", "42", It.IsAny<CancellationToken>())).ReturnsAsync(ReworkOutcome.Started("run-2"));
    }

    public void Dispose() => _store.Dispose();

    private SeriesAutoStart Sut(IReworkLaunch? launch = null)
    {
        var sp = _store.Services;
        return new SeriesAutoStart(sp.GetRequiredService<IServiceScopeFactory>(), _store.Trackers.Object,
            new TicketClosure(sp.GetRequiredService<TrackerIdentity>(), NullLogger<TicketClosure>.Instance),
            launch ?? _launch.Object,
            new ReworkSpeech(sp.GetRequiredService<IServiceScopeFactory>(), _store.Trackers.Object,
                Mock.Of<ISourceProviderFactory>(), NullLogger<ReworkSpeech>.Instance),
            _store.Activity.Object, NullLogger<SeriesAutoStart>.Instance);
    }

    private Task<ReworkNudgeDisposition> ContinueAsync(IReworkLaunch? launch = null) =>
        Sut(launch).ContinueAsync(Alpha, "42", CancellationToken.None);

    private void Ticket(string status, bool closed = false) =>
        _store.Tracker.Setup(t => t.GetTicketAsync(It.Is<TicketId>(i => i.Value == "42"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ticket(new TicketId("42"), "t", "d", null, status, "GitHub", isClosed: closed));

    // An attempt that built A of three and stopped at its chunk boundary.
    private Task ChunkEndedAsync(string status = "success", decimal cost = 5m, params object[] more) =>
        _store.AddAsync([Run(R1, status: status, cost: cost), Step(R1), Cut(R1, 3, Entry(A, 1), Entry(B, 2), Entry(C, 3)),
            SpecRow(R1, A, "done"), SpecRow(R1, B, "not_started"), SpecRow(R1, C, "not_started"), .. more]);

    private void VerifySaid(Times times) =>
        _store.Tracker.Verify(t => t.UpdateStatusAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), times);

    private void VerifyNudged(Times times) =>
        _store.Activity.Verify(a => a.ChangedAsync("alpha", "github-42", It.IsAny<CancellationToken>()), times);

    private static RunDefaultDecision HoldingDefault(string verdict = DefaultDecisionVerdicts.Pending) => new()
    {
        RunId = R1, Question = "Which queue?", Chosen = "the bus", Why = "fewer hops", HoldsSeries = true,
        Kind = DefaultDecisionKinds.Decision, TakenAt = T, Verdict = verdict,
    };

    [Fact]
    public async Task SeriesAutoStart_Start_StartsAnAttemptMarkedSeries()
    {
        await ChunkEndedAsync();

        (await ContinueAsync(new ReworkLaunch(_store.Services.GetRequiredService<IServiceScopeFactory>())))
            .Should().Be(ReworkNudgeDisposition.Finish);

        _claimed.Should().NotBeNull("the series start went through the funnel");
        _claimed!.InitialContext.IsSeriesAutoStart().Should().BeTrue();
        _claimed.InitialContext.IsRequestedByName().Should().BeTrue("every launcher start is a named start");
        BoundaryStopGate.AdmitsStart(_claimed.InitialContext).Should().BeTrue();
        VerifySaid(Times.Never());
    }

    [Fact]
    public async Task SeriesAutoStart_StartsFromAWaitingLabelledTicket()
    {
        await ChunkEndedAsync();

        (await ContinueAsync()).Should().Be(ReworkNudgeDisposition.Finish);

        _launch.Verify(l => l.StartSeriesAttemptAsync("alpha", "42", It.IsAny<CancellationToken>()), Times.Once);
        _launch.Verify(l => l.LaunchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SeriesAutoStart_RatifyOfTheHoldingDefault_NudgeRechecksAndStarts()
    {
        await ChunkEndedAsync(more: HoldingDefault());

        await ContinueAsync();
        _launch.Verify(l => l.StartSeriesAttemptAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifySaid(Times.Once());

        var row = (await _store.ReadAllAsync<RunDefaultDecision>()).Single();
        await _store.Verdicts.WriteAsync("alpha", "42", [new(row.Id, DefaultDecisionVerdicts.Ratified, null)],
            "ann", CancellationToken.None);
        _store.Nudges.Verify(n => n.EnqueueAsync(It.Is<ReworkNudgeRequest>(r => r.Origin == ReworkNudgeOrigin.Operator && r.TicketId == "42"),
            It.IsAny<CancellationToken>()), Times.Once, "the ratify queues the nudge that reaches this tail again");
        await ContinueAsync();

        _launch.Verify(l => l.StartSeriesAttemptAsync("alpha", "42", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SeriesAutoStart_PersonClosedTicket_StartsNothingSaysNothingAndMarksClosed()
    {
        await ChunkEndedAsync();
        Ticket("Closed", closed: true);

        (await ContinueAsync()).Should().Be(ReworkNudgeDisposition.Finish);
        await ContinueAsync();

        _launch.Verify(l => l.StartSeriesAttemptAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifySaid(Times.Never());
        VerifyNudged(Times.Once());
        (await RunwayAsync()).View.HeldBecause.Should().Be(SeriesHeldReasons.Closed, "the page reads the worker's mark");
    }

    [Fact]
    public async Task SeriesAutoStart_RefusedLaunch_MarksAndNudgesThePage()
    {
        await ChunkEndedAsync();
        _launch.Setup(l => l.StartSeriesAttemptAsync("alpha", "42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReworkOutcome.Refused("the capacity queue is full"));

        await ContinueAsync();
        await ContinueAsync();

        _launch.Verify(l => l.StartSeriesAttemptAsync("alpha", "42", It.IsAny<CancellationToken>()), Times.Once);
        _store.Tracker.Verify(t => t.UpdateStatusAsync(It.IsAny<TicketId>(),
            It.Is<string>(s => s.Contains("could not be started: the capacity queue is full")), It.IsAny<CancellationToken>()), Times.Once);
        VerifyNudged(Times.Once());
        (await RunwayAsync()).View.HeldBecause.Should().Be(SeriesHeldReasons.Refused);
    }

    [Fact]
    public async Task SeriesAutoStart_TrackerUnreadable_Reschedules()
    {
        await ChunkEndedAsync();
        _store.Tracker.Setup(t => t.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("503"));

        (await ContinueAsync()).Should().Be(ReworkNudgeDisposition.Reschedule);

        _launch.Verify(l => l.StartSeriesAttemptAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SeriesAutoStart_RunwayHold_IsSaidOnceAndNudges_WithoutRetry()
    {
        await ChunkEndedAsync(cost: 150m);
        string? said = null;
        _store.Tracker.Setup(t => t.UpdateStatusAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<TicketId, string, CancellationToken>((_, text, _) => said = text).Returns(Task.CompletedTask);

        (await ContinueAsync()).Should().Be(ReworkNudgeDisposition.Finish);
        await ContinueAsync();

        VerifySaid(Times.Once());
        VerifyNudged(Times.Once());
        said.Should().Contain("series.budget_usd").And.Contain("$150.00 of $150.00").And.Contain("2 spec(s) left")
            .And.Contain(SeriesTexts.HowToGoOn).And.NotContainEquivalentOf("retry");
        _launch.Verify(l => l.StartSeriesAttemptAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SeriesAutoStart_AttemptEndHold_IsNotSaid()
    {
        await ChunkEndedAsync(status: "failed");

        (await ContinueAsync()).Should().Be(ReworkNudgeDisposition.Finish);

        VerifySaid(Times.Never());
        VerifyNudged(Times.Never());
        (await RunwayAsync()).View.HeldBecause.Should().Be(SeriesHeldReasons.Incomplete);
    }

    private async Task<SeriesRunwayRead> RunwayAsync()
    {
        using var scope = _store.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<SeriesRunwayReader>().ReadAsync("alpha", "42", CancellationToken.None))!;
    }

    // The real launcher over the real funnel; the claim service is the double, because the claim is where the run leaves.
    private ChatTicketRunLauncher Launcher()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>()))
            .Returns(new AgentSmithConfig { Projects = new Dictionary<string, ResolvedProject> { ["alpha"] = Alpha } });
        var claims = new Mock<ITicketClaimService>();
        claims.Setup(c => c.ClaimAsync(It.IsAny<ClaimRequest>(), It.IsAny<AgentSmithConfig>(), It.IsAny<CancellationToken>()))
            .Callback<ClaimRequest, AgentSmithConfig, CancellationToken>((r, _, _) => _claimed = r)
            .ReturnsAsync(ClaimResult.Claimed());
        var funnel = new SpawnPipelineRunsUseCase(
            claims.Object, CapacityTestDoubles.StubCalculator(), CapacityTestDoubles.AlwaysReserve(),
            CapacityTestDoubles.EmptyQueue(), CapacityTestDoubles.NoCorpses(), CapacityTestDoubles.NoHolds(),
            CapacityTestDoubles.AlwaysAdmit(), ApprovedSetDoubles.Carrier(), CapacityTestDoubles.NoNudge(),
            CapacityTestDoubles.NoStandingRefusal(), Mock.Of<IActiveRunLease>(), NullLogger<SpawnPipelineRunsUseCase>.Instance,
            new AgentSmith.Application.Services.Resume.NullPreviousAttemptReader());
        return new ChatTicketRunLauncher(loader.Object, new ServerContext("c.yml"), funnel, NullLogger<ChatTicketRunLauncher>.Instance);
    }
}
