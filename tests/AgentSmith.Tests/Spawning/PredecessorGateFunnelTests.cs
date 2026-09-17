using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Spawning;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Spawning;

/// <summary>
/// 2026-09-13-a72a: the spawn funnel asks, FIRST, whether the slices this ticket follows
/// have left the working set. "Left" is "outside trigger_statuses", not "equals
/// done_status" — done_status defaults to "In Review" (a pull request was opened) and an
/// operator closing a ticket by hand to another terminal status must still release its
/// successor. A held child reserves nothing and enqueues nothing: the capacity queue is
/// strict FIFO across every project, so a dependency at its head would stall the estate.
/// </summary>
public sealed class PredecessorGateFunnelTests
{
    private const string Open = "To Do";
    private const string Done = "Closed";

    [Fact]
    public async Task SpawnFunnel_PredecessorInTriggerStatuses_DoesNotClaim()
    {
        var harness = new Harness(("7", Open, []));

        var result = await harness.SpawnAsync(Follows("7"));

        harness.ClaimCount.Should().Be(0);
        result.ClaimResults.Should().ContainSingle()
            .Which.Outcome.Should().Be(ClaimOutcome.Queued);
        result.ClaimResults[0].Error.Should().Contain("7").And.Contain(Open,
            "the tracker must say which slice it is waiting for and why");
    }

    [Fact]
    public async Task SpawnFunnel_PredecessorOutsideTriggerStatuses_Claims()
    {
        var harness = new Harness(("7", Done, []));

        var result = await harness.SpawnAsync(Follows("7"));

        harness.ClaimCount.Should().Be(1);
        result.ClaimResults[0].Outcome.Should().Be(ClaimOutcome.Claimed);
    }

    [Fact]
    public async Task SpawnFunnel_PredecessorOpen_EnqueuesNothingAndReservesNothing()
    {
        var harness = new Harness(("7", Open, []));

        await harness.SpawnAsync(Follows("7"));

        harness.Recorded.Should().BeEmpty("a held child must hold no budget record");
        harness.Enqueued.Should().Be(0, "and no queued Run row — the gate runs before both");
    }

    [Fact]
    public async Task SpawnFunnel_HopBoundExceeded_ReportedAndNotWaitedOn()
    {
        // A hand-edited cycle through RESOLVED tickets: nothing blocks, and without the
        // bound the walk never ends. RequiresEdgeChecker refuses a cyclic epic at dialogue
        // time, so this is the only way one gets in.
        var harness = new Harness(
            ("7", Done, new[] { FiledTicketLabels.PredecessorStamp("8") }),
            ("8", Done, new[] { FiledTicketLabels.PredecessorStamp("7") }));

        var result = await harness.SpawnAsync(Follows("7"));

        result.ClaimResults[0].Outcome.Should().Be(ClaimOutcome.Claimed,
            "an unresolvable chain is reported and not waited on — blocked forever is worse "
            + "than out of order");
        harness.Reads.Should().Be(16, "the walk stops at the hop bound instead of spinning");
    }

    [Fact]
    public async Task SpawnFunnel_TicketWithoutPredecessorLabels_ClaimsAsBefore()
    {
        var harness = new Harness();

        var result = await harness.SpawnAsync(new IncomingTicketEnvelope
        {
            TicketId = "42", Platform = "github", Labels = ["agent-smith"],
        });

        harness.ClaimCount.Should().Be(1);
        harness.Reads.Should().Be(0, "a ticket that names no predecessor costs no tracker read");
        result.ClaimResults[0].Outcome.Should().Be(ClaimOutcome.Claimed);
    }

    private static IncomingTicketEnvelope Follows(params string[] predecessorIds) =>
        new()
        {
            TicketId = "42",
            Platform = "github",
            Labels = [PhaseTicketRenderer.PhaseLabel,
                      FiledTicketLabels.ParentStamp("1"),
                      .. predecessorIds.Select(FiledTicketLabels.PredecessorStamp)],
        };

    private sealed class Harness
    {
        private readonly SpawnPipelineRunsUseCase _sut;
        private readonly StubProvider _provider;

        public int ClaimCount { get; private set; }
        public List<string> Recorded { get; } = [];
        public int Enqueued { get; private set; }
        public int Reads => _provider.Reads;

        public Harness(params (string Id, string Status, IReadOnlyList<string> Labels)[] tickets)
        {
            _provider = new StubProvider(tickets.ToDictionary(
                t => t.Id,
                t => new Ticket(new TicketId(t.Id), $"#{t.Id}", "body", null, t.Status, "stub", t.Labels),
                StringComparer.Ordinal));
            var factory = new Mock<ITicketProviderFactory>();
            factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(_provider);

            var claimService = new Mock<ITicketClaimService>();
            claimService.Setup(c => c.ClaimAsync(
                    It.IsAny<ClaimRequest>(), It.IsAny<AgentSmithConfig>(), It.IsAny<CancellationToken>()))
                .Callback(() => ClaimCount++)
                .ReturnsAsync(ClaimResult.Claimed());

            var budget = new Mock<ICapacityBudget>();
            budget.Setup(b => b.RecordAsync(
                    It.IsAny<string>(), It.IsAny<RunFootprintBreakdown>(), It.IsAny<CancellationToken>()))
                .Callback<string, RunFootprintBreakdown, CancellationToken>((id, _, _) => Recorded.Add(id))
                .Returns(Task.CompletedTask);
            budget.Setup(b => b.TryReserveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var queue = new Mock<ICapacityQueue>();
            queue.Setup(q => q.PeekHeadAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((CapacityQueueEntry?)null);
            queue.Setup(q => q.EnqueueAsync(
                    It.IsAny<CapacityQueueCandidate>(), It.IsAny<CancellationToken>()))
                .Callback(() => Enqueued++)
                .ReturnsAsync("run-queued");

            _sut = new SpawnPipelineRunsUseCase(
                claimService.Object, CapacityTestDoubles.StubCalculator(), budget.Object,
                queue.Object, CapacityTestDoubles.NoCorpses(), CapacityTestDoubles.AlwaysAdmit(),
                new PredecessorGate(factory.Object, NullLogger<PredecessorGate>.Instance),
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

    private sealed class StubProvider(IReadOnlyDictionary<string, Ticket> tickets) : ITicketProvider
    {
        public int Reads { get; private set; }

        public string ProviderType => "stub";

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ConnectionProbeResult.Reachable(0));

        public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(tickets[ticketId.Value]);
        }

        public Task<CreatedTicket> CreateAsync(
            string title, string description, IReadOnlyList<string> labels,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ParentLinkResult> LinkToParentAsync(
            CreatedTicket child, TicketId parent, CancellationToken cancellationToken) =>
            Task.FromResult(ParentLinkResult.Unsupported("this fake has no relations"));

        public Task FinalizeAsync(
            TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
