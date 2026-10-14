using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Spawning;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.ChatLaunch;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Spawning;

/// <summary>
/// 2026-10-14-dbceh: a ticket held by a required ticket that has not landed is refused by the funnel before
/// the footprint, the queue peek and the reservation — at every poll, behind a full queue, and when a person
/// starts it by name in chat, whose answer names what it waits for.
/// </summary>
public sealed class SpawnRequiresGateTests
{
    private const string Waits = "This ticket waits for #41 to be merged first; merge them, or mark them done by hand.";
    private readonly InMemoryTicketFacts _facts = new();
    private readonly InMemorySpecApprovalStore _approvals = new();
    private readonly Mock<ITicketClaimService> _claims = new();
    private readonly Mock<IRunFootprintCalculator> _footprint = new();
    private readonly Mock<ICapacityBudget> _budget = new();
    private readonly Mock<ICapacityQueue> _queue = new();
    private readonly Mock<ISandboxCapacityProbe> _probe = new();

    public SpawnRequiresGateTests()
    {
        _facts.Edges.Add(new TicketRequirementEdge("gh", "github-42", "42", "sample", "github-41", "41", true, default, "s-1"));
        _approvals.SaveAsync(ApprovedSets.Record("github-42", DateTimeOffset.UnixEpoch, tracker: "gh"), default).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Spawn_HeldTicket_IsRefusedBeforeAnyFootprintOrReservation()
    {
        var result = await SpawnAsync();

        result.ClaimResults.Should().ContainSingle().Which.Error.Should().Be(Waits);
        _footprint.VerifyNoOtherCalls();
        _budget.VerifyNoOtherCalls();
        _probe.VerifyNoOtherCalls();
        _queue.VerifyNoOtherCalls();
        _claims.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Spawn_BehindANonEmptyQueue_HeldTicket_IsRefusedNotQueued()
    {
        _queue.Setup(q => q.PeekHeadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new CapacityQueueEntry(
            "sample", "7", "code", "github", "run-head", "waiting", DateTimeOffset.UnixEpoch, null, null));

        var result = await SpawnAsync();

        result.ClaimResults.Single().Rejection.Should().Be(ClaimRejectionReason.TicketRequiresUnmet);
        _queue.Verify(q => q.EnqueueAsync(It.IsAny<CapacityQueueCandidate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Poll_HeldTicket_IsRefusedEveryCycleAndNeverQueued()
    {
        for (var cycle = 0; cycle < 3; cycle++)
            (await SpawnAsync()).ClaimResults.Single().Outcome.Should().Be(ClaimOutcome.Rejected);

        _queue.Verify(q => q.EnqueueAsync(It.IsAny<CapacityQueueCandidate>(), It.IsAny<CancellationToken>()), Times.Never);
        _budget.Verify(b => b.RecordAsync(It.IsAny<string>(), It.IsAny<RunFootprintBreakdown>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChatStartByName_HeldTicket_ChatSaysWhichTicketsItWaitsFor()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject> { ["sample"] = Project },
        });
        var launcher = new ChatTicketRunLauncher(loader.Object, new ServerContext("c.yml"), Funnel(),
            NullLogger<ChatTicketRunLauncher>.Instance);

        var result = await launcher.LaunchAsync("sample", "#42", "code", CancellationToken.None);

        result.Refusal.Should().Be(Waits);
        result.RunId.Should().BeNull();
    }

    private static ResolvedProject Project { get; } = new()
    {
        Name = "sample",
        Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
        Repos = [new RepoConnection { Name = "api" }],
        GithubTrigger = new WebhookTriggerConfig { DefaultPipeline = "code" },
    };

    private Task<SpawnResult> SpawnAsync() => Funnel().ExecuteAsync(
        new AgentSmithConfig { Projects = new Dictionary<string, ResolvedProject> { ["sample"] = Project } }, Project, "code",
        new IncomingTicketEnvelope { TicketId = "42", Platform = "github" }, new WebhookTriggerConfig(), CancellationToken.None);

    private SpawnPipelineRunsUseCase Funnel() => new(
        _claims.Object, _footprint.Object, _budget.Object, _queue.Object, CapacityTestDoubles.NoCorpses(), CapacityTestDoubles.NoHolds(),
        _probe.Object, ApprovedSetDoubles.Carrier(), CapacityTestDoubles.NoNudge(), CapacityTestDoubles.NoStandingRefusal(),
        Mock.Of<IActiveRunLease>(), NullLogger<SpawnPipelineRunsUseCase>.Instance,
        new AgentSmith.Application.Services.Resume.NullPreviousAttemptReader(), TicketRequirementDoubles.Of(_facts, _approvals));
}
