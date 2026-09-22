using AgentSmith.Application.Services.Spawning;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Spawning;

/// <summary>
/// 2026-09-22-766b: the spawn funnel no longer asks whether the slices a ticket follows have left
/// the working set. Nothing the framework files has carried a predecessor stamp since
/// 2026-09-17-0e79d — an approved cut is ONE work ticket and the order inside it is the
/// sequence's, a successor held until its predecessor VERIFIED, which is stronger than any status
/// gate — and the gate could only ever HOLD a spawn, never refuse one.
/// <para>
/// THE HONEST COST IS THE SECOND TEST. Two legacy children of the withdrawn N-children shape may
/// now run CONCURRENTLY and cut from the same parent rung. Nothing files such a pair any more;
/// the ones on a board were filed before that shape was withdrawn.
/// </para>
/// <para>
/// The labels here are written out rather than composed, because the prefix has no factory left:
/// <c>phase-requires:</c> is a legacy word this framework reads nowhere.
/// </para>
/// </summary>
public sealed class PredecessorRetirementTests
{
    private const string Open = "To Do";

    [Fact]
    public async Task Spawn_ATicketCarryingAPredecessorStamp_IsNoLongerHeld()
    {
        var harness = new Harness();

        var result = await harness.SpawnAsync(Legacy("42", "phase-requires:7"));

        harness.ClaimCount.Should().Be(1, "the stamp holds nothing back any more");
        result.ClaimResults.Should().ContainSingle()
            .Which.Outcome.Should().Be(ClaimOutcome.Claimed,
                "a predecessor still in a trigger status used to queue this ticket with a wait "
                + "reason; it now starts, and the predecessor's own status is nobody's gate");
    }

    [Fact]
    public async Task Spawn_TwoLegacyChildrenOfOneParent_MayRunConcurrently()
    {
        var harness = new Harness();

        var first = await harness.SpawnAsync(
            Legacy("42", "phase-parent:1"));
        var second = await harness.SpawnAsync(
            Legacy("43", "phase-parent:1", "phase-requires:42"));

        harness.ClaimCount.Should().Be(2);
        first.ClaimResults[0].Outcome.Should().Be(ClaimOutcome.Claimed);
        second.ClaimResults[0].Outcome.Should().Be(ClaimOutcome.Claimed,
            "this is what retiring the gate costs: the second child no longer waits for the "
            + "first, so both run at once and both cut from the same parent rung");
    }

    private static IncomingTicketEnvelope Legacy(string ticketId, params string[] labels) =>
        new() { TicketId = ticketId, Platform = "github", Labels = labels };

    private sealed class Harness
    {
        private readonly SpawnPipelineRunsUseCase _sut;

        public int ClaimCount { get; private set; }

        public Harness()
        {
            var claimService = new Mock<ITicketClaimService>();
            claimService.Setup(c => c.ClaimAsync(
                    It.IsAny<ClaimRequest>(), It.IsAny<AgentSmithConfig>(), It.IsAny<CancellationToken>()))
                .Callback(() => ClaimCount++)
                .ReturnsAsync(ClaimResult.Claimed());

            var budget = new Mock<ICapacityBudget>();
            budget.Setup(b => b.RecordAsync(
                    It.IsAny<string>(), It.IsAny<RunFootprintBreakdown>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            budget.Setup(b => b.TryReserveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var queue = new Mock<ICapacityQueue>();
            queue.Setup(q => q.PeekHeadAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((CapacityQueueEntry?)null);

            _sut = new SpawnPipelineRunsUseCase(
                claimService.Object, CapacityTestDoubles.StubCalculator(), budget.Object,
                queue.Object, CapacityTestDoubles.NoCorpses(), CapacityTestDoubles.NoHolds(),
                CapacityTestDoubles.AlwaysAdmit(),
                TestSupport.ApprovedSetDoubles.Carrier(),
                CapacityTestDoubles.NoNudge(),
                CapacityTestDoubles.NoStandingRefusal(),
                NullLogger<SpawnPipelineRunsUseCase>.Instance);
        }

        public Task<SpawnResult> SpawnAsync(IncomingTicketEnvelope envelope) =>
            _sut.ExecuteAsync(
                new AgentSmithConfig(),
                new ResolvedProject
                {
                    Name = "proj",
                    Repos = [new RepoConnection { Name = "repo-a" }],
                    GithubTrigger = new WebhookTriggerConfig { TriggerStatuses = [Open] },
                },
                "fix-bug", envelope, new WebhookTriggerConfig { TriggerStatuses = [Open] },
                CancellationToken.None);
    }
}
