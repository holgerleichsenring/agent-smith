using AgentSmith.Application.Services.Orchestrator;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Spawning;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Spawning;

/// <summary>
/// SpawnPipelineRunsUseCase builds exactly one ClaimRequest per ticket and submits
/// it through ITicketClaimService.ClaimAsync. The unified-run model: regardless of
/// how many repos the project has, one ticket = one pipeline run = one enqueue.
/// </summary>
public sealed class SpawnPipelineRunsUseCaseTests
{
    private static readonly AgentSmithConfig EmptyConfig = new();

    [Fact]
    public async Task SpawnPipelineRuns_OneRepo_EnqueuesOneRun()
    {
        var harness = new Harness();
        var project = BuildProject("p1", repos: new[] { "repo-only" });

        await harness.Sut.ExecuteAsync(
            EmptyConfig, project, "fix-bug", Envelope("42"), Trigger(), CancellationToken.None);

        harness.CallCount.Should().Be(1);
        harness.LastRequest.Should().NotBeNull();
        harness.LastRequest!.ProjectName.Should().Be("p1");
        harness.LastRequest.PipelineName.Should().Be("fix-bug");
        harness.LastRequest.TicketId.Value.Should().Be("42");
    }

    [Fact]
    public async Task SpawnPipelineRuns_ThreeRepos_StillEnqueuesExactlyOneRequest_NoFanOut()
    {
        var harness = new Harness();
        var project = BuildProject("p1", repos: new[] { "repo-a", "repo-b", "repo-c" });

        await harness.Sut.ExecuteAsync(
            EmptyConfig, project, "fix-bug", Envelope("42"), Trigger(), CancellationToken.None);

        harness.CallCount.Should().Be(1);
        harness.LastRequest.Should().NotBeNull();
        harness.LastRequest!.TicketId.Value.Should().Be("42");
        harness.LastRequest.PipelineName.Should().Be("fix-bug");
    }

    [Fact]
    public async Task SpawnPipelineRuns_PipelineNameFlowsThroughUnchanged()
    {
        var harness = new Harness();
        var project = BuildProject("p1", repos: new[] { "repo-a" }) with
        {
            Pipelines = new List<PipelineDefinition>
            {
                new() { Name = "fix-bug", AgentName = "override-agent",
                        Agent = new AgentConfig { Type = "claude", Model = "claude-opus" } }
            }
        };

        await harness.Sut.ExecuteAsync(
            EmptyConfig, project, "fix-bug", Envelope("42"), Trigger(), CancellationToken.None);

        harness.LastRequest.Should().NotBeNull();
        harness.LastRequest!.PipelineName.Should().Be("fix-bug");
    }

    [Fact]
    public async Task SpawnPipelineRuns_InitialContextCarriesDoneStatusFromMatchedTrigger()
    {
        var harness = new Harness();
        var project = BuildProject("p1", repos: new[] { "repo-a" });
        var trigger = new WebhookTriggerConfig { DoneStatus = "In Review" };

        await harness.Sut.ExecuteAsync(
            EmptyConfig, project, "fix-bug", Envelope("42"), trigger, CancellationToken.None);

        harness.LastRequest.Should().NotBeNull();
        harness.LastRequest!.InitialContext.Should().NotBeNull();
        harness.LastRequest.InitialContext![ContextKeys.DoneStatus].Should().Be("In Review");
    }

    [Fact]
    public async Task SpawnPipelineRuns_InitialContextCarriesFailedStatusFromMatchedTrigger()
    {
        // p0261: failed_status is seeded so the failure path can terminalize the native status.
        var harness = new Harness();
        var project = BuildProject("p1", repos: new[] { "repo-a" });
        var trigger = new WebhookTriggerConfig { DoneStatus = "In Review", FailedStatus = "Blocked" };

        await harness.Sut.ExecuteAsync(
            EmptyConfig, project, "fix-bug", Envelope("42"), trigger, CancellationToken.None);

        harness.LastRequest!.InitialContext![ContextKeys.FailedStatus].Should().Be("Blocked");
    }

    [Fact]
    public async Task SpawnPipelineRuns_FailedStatusUnset_FallsBackToDoneStatusInContext()
    {
        // p0261: failed_status unset → falls back to done_status, so a failed run still
        // terminalizes (the ticket never stays New/Active).
        var harness = new Harness();
        var project = BuildProject("p1", repos: new[] { "repo-a" });
        var trigger = new WebhookTriggerConfig { DoneStatus = "Resolved" }; // FailedStatus null

        await harness.Sut.ExecuteAsync(
            EmptyConfig, project, "fix-bug", Envelope("42"), trigger, CancellationToken.None);

        harness.LastRequest!.InitialContext![ContextKeys.FailedStatus].Should().Be("Resolved");
    }

    private static ResolvedProject BuildProject(string name, string[] repos) => new()
    {
        Name = name,
        Repos = repos.Select(r => new RepoConnection { Name = r }).ToList(),
    };

    private static IncomingTicketEnvelope Envelope(string ticketId) =>
        new() { TicketId = ticketId, Platform = "github" };

    private static WebhookTriggerConfig Trigger() =>
        new() { DefaultPipeline = "fix-bug", DoneStatus = "closed" };

    // p0336: a run whose footprint does not fit the budget defers WITHOUT claiming
    // — the ticket queues (visible) and retries via the pump, which is how two runs
    // that don't fit together are processed sequentially.
    [Fact]
    public async Task SpawnPipelineRuns_FootprintDoesNotFit_ReturnsQueuedDoesNotClaim()
    {
        var harness = new Harness(fits: false);
        var project = BuildProject("p1", repos: new[] { "repo-only" });

        var result = await harness.Sut.ExecuteAsync(
            EmptyConfig, project, "fix-bug", Envelope("42"), Trigger(), CancellationToken.None);

        harness.CallCount.Should().Be(0, "a run that does not fit the budget must not be claimed");
        result.ClaimResults.Should().ContainSingle()
            .Which.Outcome.Should().Be(ClaimOutcome.Queued);
    }

    // p0355: after the at-claim corpse reap, admission reconciles with the REAL
    // namespace ResourceQuota — a run k8s cannot fit is QUEUED, not admitted then
    // killed with "exceeded quota", EVEN when the internal budget would reserve it.
    [Fact]
    public async Task Admission_AfterReap_NamespaceQuotaFull_RunQueuedNotAdmitted()
    {
        var harness = new Harness(fits: true, quotaProbe: CapacityTestDoubles.AlwaysDeny());
        var project = BuildProject("p1", repos: new[] { "repo-only" });

        var result = await harness.Sut.ExecuteAsync(
            EmptyConfig, project, "fix-bug", Envelope("42"), Trigger(), CancellationToken.None);

        harness.CallCount.Should().Be(0, "a run the namespace quota cannot fit must not be claimed");
        result.ClaimResults.Should().ContainSingle()
            .Which.Outcome.Should().Be(ClaimOutcome.Queued);
    }

    [Fact]
    public async Task SpawnPipelineRuns_FootprintFits_ClaimsAsBefore()
    {
        var harness = new Harness(fits: true);
        var project = BuildProject("p1", repos: new[] { "repo-only" });

        var result = await harness.Sut.ExecuteAsync(
            EmptyConfig, project, "fix-bug", Envelope("42"), Trigger(), CancellationToken.None);

        harness.CallCount.Should().Be(1);
        result.ClaimResults.Should().ContainSingle()
            .Which.Outcome.Should().Be(ClaimOutcome.Claimed);
    }

    // p0336: the run's computed footprint is RECORDED (for the dashboard) before the
    // budget reservation is attempted, keyed by the run id that will be claimed.
    [Fact]
    public async Task Spawn_RecordsComputedFootprint_BeforeReserving()
    {
        var footprint = new RunFootprintBreakdown(
            [new RunFootprintPod("repo-a", ["default"], "dotnet", "1", "4Gi")],
            "1", "4Gi", 1_000_000_000, 4L * 1024 * 1024 * 1024, [], "1 pod");
        var harness = new Harness(fits: true, footprint: footprint);
        var project = BuildProject("p1", repos: new[] { "repo-a" });

        await harness.Sut.ExecuteAsync(
            EmptyConfig, project, "fix-bug", Envelope("42"), Trigger(), CancellationToken.None);

        harness.RecordedFootprint.Should().BeSameAs(footprint);
        harness.RecordedRunId.Should().NotBeNullOrEmpty();
    }

    private sealed class Harness
    {
        public SpawnPipelineRunsUseCase Sut { get; }
        public int CallCount { get; private set; }
        public ClaimRequest? LastRequest { get; private set; }
        public RunFootprintBreakdown? RecordedFootprint { get; private set; }
        public string? RecordedRunId { get; private set; }

        public Harness(bool fits = true, RunFootprintBreakdown? footprint = null,
            ISandboxCapacityProbe? quotaProbe = null)
        {
            var claimService = new Mock<ITicketClaimService>();
            claimService.Setup(c => c.ClaimAsync(
                    It.IsAny<ClaimRequest>(),
                    It.IsAny<AgentSmithConfig>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ClaimRequest, AgentSmithConfig, CancellationToken>(
                    (r, _, _) => { CallCount++; LastRequest = r; })
                .ReturnsAsync(ClaimResult.Claimed());

            var fp = footprint ?? new RunFootprintBreakdown(
                [], "1", "4Gi", 1_000_000_000, 4L * 1024 * 1024 * 1024, [], "stub");
            var calculator = new Mock<IRunFootprintCalculator>();
            calculator.Setup(c => c.CalculateAsync(
                    It.IsAny<ResolvedProject>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fp);

            var budget = new Mock<ICapacityBudget>();
            budget.Setup(b => b.RecordAsync(
                    It.IsAny<string>(), It.IsAny<RunFootprintBreakdown>(), It.IsAny<CancellationToken>()))
                .Callback<string, RunFootprintBreakdown, CancellationToken>(
                    (id, f, _) => { RecordedRunId = id; RecordedFootprint = f; })
                .Returns(Task.CompletedTask);
            budget.Setup(b => b.TryReserveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fits);
            budget.Setup(b => b.ReleaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            Sut = new SpawnPipelineRunsUseCase(
                claimService.Object, calculator.Object, budget.Object,
                CapacityTestDoubles.EmptyQueue(),
                CapacityTestDoubles.NoCorpses(), quotaProbe ?? CapacityTestDoubles.AlwaysAdmit(),
                NullLogger<SpawnPipelineRunsUseCase>.Instance);
        }
    }
}
