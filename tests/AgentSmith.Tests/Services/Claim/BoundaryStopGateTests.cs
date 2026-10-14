using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Claim;

/// <summary>2026-10-13-8762g: a ticket whose last attempt stopped at a boundary is not claimed again by the poller,
/// a webhook or the pump; a person's start is; the one end rule says which ends are a boundary.</summary>
public sealed class BoundaryStopGateTests
{
    [Fact]
    public async Task Poll_TicketStoppedAtBoundary_IsNotReclaimed()
    {
        var (sut, claimLock) = Service(Stopped(true));

        var result = await sut.ClaimAsync(Request(), Config(), CancellationToken.None);

        result.Outcome.Should().Be(ClaimOutcome.Rejected);
        result.Rejection.Should().Be(ClaimRejectionReason.TicketStoppedAtBoundary);
        claimLock.Verify(l => l.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never, "a refused claim takes no lock and writes nothing");
    }

    [Fact]
    public async Task Claim_PersonsStartOfATicketStoppedAtBoundary_IsAdmitted()
    {
        var attempts = Stopped(true);
        var (sut, claimLock) = Service(attempts);
        var byName = Request() with { InitialContext = new() { [ContextKeys.RequestedByName] = true } };

        var result = await sut.ClaimAsync(byName, Config(), CancellationToken.None);

        result.Rejection.Should().NotBe(ClaimRejectionReason.TicketStoppedAtBoundary);
        claimLock.Verify(l => l.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once, "a person's start goes on to the claim itself");
        BoundaryStopGate.AdmitsStart(byName.InitialContext).Should().BeTrue();
    }

    // 2026-10-13-8762v: the series' automatic start is admitted, and not only through the launcher's name flag.
    [Fact]
    public async Task BoundaryClaimGate_SeriesAutoStart_IsAdmitted()
    {
        var series = Request() with
        {
            InitialContext = new() { [ContextKeys.RequestedByName] = true, [ContextKeys.SeriesAutoStart] = true },
        };

        (await new BoundaryStopGate(Stopped(true).Object).RefusalAsync(series, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task BoundaryClaimGate_SeriesAutoStartWithoutRequestedByName_IsStillAdmitted()
    {
        var queued = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>("{\"SeriesAutoStart\":true}")!;
        var series = Request() with { InitialContext = queued };
        var (sut, _) = Service(Stopped(true));

        BoundaryStopGate.AdmitsStart(queued).Should().BeTrue("the flag is read in its queue-row shape too");
        (await sut.ClaimAsync(series, Config(), CancellationToken.None)).Rejection
            .Should().NotBe(ClaimRejectionReason.TicketStoppedAtBoundary);
    }

    [Fact]
    public async Task BoundaryStopGate_Refusal_NamesAStartByNameOrACommentAndNotRetry()
    {
        var refusal = await new BoundaryStopGate(Stopped(true).Object).RefusalAsync(Request(), CancellationToken.None);

        refusal!.Error.Should().Be(BoundaryStopGate.Sentence);
        BoundaryStopGate.Sentence.Should().Contain("start it by name in chat").And.Contain("a comment on the ticket")
            .And.NotContainEquivalentOf("retry");
        (await new BoundaryStopGate(Stopped(false).Object).RefusalAsync(Request(), CancellationToken.None))
            .Should().BeNull();
    }

    [Fact]
    public void SeriesBoundaryEnd_SuccessAndWatchdogCancelHold_ShortfallFailureAndOperatorCancelDoNot()
    {
        SeriesBoundaryEnd.Ends(RunStatuses.Success, null).Should().BeTrue();
        SeriesBoundaryEnd.Ends("cancelled", "watchdog").Should().BeTrue();
        SeriesBoundaryEnd.Ends("cancelled", "watchdog-wall-time").Should().BeTrue();
        SeriesBoundaryEnd.Ends(RunStatuses.Shortfall, null).Should().BeFalse();
        SeriesBoundaryEnd.Ends(RunStatuses.Failed, null).Should().BeFalse();
        SeriesBoundaryEnd.Ends("cancelled", "operator").Should().BeFalse();
        SeriesBoundaryEnd.Ends("cancelled", null).Should().BeFalse();
    }

    private static Mock<IPreviousAttemptReader> Stopped(bool stopped)
    {
        var attempts = new Mock<IPreviousAttemptReader>();
        attempts.Setup(a => a.LatestAsync("my-project", "42", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("run-1", RunStatuses.Success, DateTimeOffset.UtcNow, true)
            {
                StoppedAtBoundary = stopped,
            });
        return attempts;
    }

    private static (TicketClaimService Sut, Mock<IRedisClaimLock> Lock) Service(Mock<IPreviousAttemptReader> attempts)
    {
        var claimLock = new Mock<IRedisClaimLock>();
        return (new TicketClaimService(claimLock.Object, new InMemoryUnmovedTicketStore(), attempts.Object,
            Mock.Of<ITicketStatusTransitionerFactory>(), Mock.Of<IRedisJobQueue>(), Mock.Of<IActiveRunLease>(),
            new InMemoryTakenTicketStore(), NullLogger<TicketClaimService>.Instance), claimLock);
    }

    private static ClaimRequest Request() => new("GitHub", "my-project", new TicketId("42"), "code");

    private static AgentSmithConfig Config() => new()
    {
        Projects = new()
        {
            ["my-project"] = new ResolvedProject { GithubTrigger = new WebhookTriggerConfig { DefaultPipeline = "code" } },
        },
    };
}
