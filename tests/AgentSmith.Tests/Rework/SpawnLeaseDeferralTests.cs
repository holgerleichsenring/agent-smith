using AgentSmith.Application.Services.Spawning;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Spawning;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-08-e8b9e: under capacity pressure a ticket with a live run is answered, not queued.</summary>
public sealed class SpawnLeaseDeferralTests
{
    [Fact]
    public async Task Admission_TicketedCommandLiveLeaseUnderCapacityPressure_QueuesNothing()
    {
        var queue = new Mock<ICapacityQueue>();
        var leases = new Mock<IActiveRunLease>();
        leases.Setup(l => l.GetByTicketAsync("p", It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaleLease("p", new TicketId("12"), "run-live", null, DateTimeOffset.UtcNow));
        var deferral = new SpawnLeaseDeferral(
            new CapacityDeferral(queue.Object, Mock.Of<ICapacityBudget>(), TestSupport.ApprovedSetDoubles.Carrier(),
                CapacityTestDoubles.NoNudge(), NullLogger.Instance),
            CapacityTestDoubles.NoStandingRefusal(), leases.Object, TimeProvider.System, NullLogger.Instance);
        var project = new ResolvedProject { Name = "p", Tracker = new TrackerConnection { Type = TrackerType.GitHub } };
        var config = new AgentSmithConfig { Projects = new Dictionary<string, ResolvedProject> { ["p"] = project } };

        var result = await deferral.DeferAsync(config, project, "code",
            new IncomingTicketEnvelope { TicketId = "12", Platform = "github", RequestedByName = true },
            new WebhookTriggerConfig(), null, RunFootprintBreakdown.Empty, null, "run-x", null, CancellationToken.None);

        result.ClaimResults.Single().Outcome.Should().Be(ClaimOutcome.AlreadyClaimed);
        queue.VerifyNoOtherCalls();
    }
}
