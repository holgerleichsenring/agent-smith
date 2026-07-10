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

    // p0269a: capacity-exhausted admission defers WITHOUT claiming — the ticket stays
    // reclaimable so the next poll retries, which is how two tickets that don't fit
    // together are processed sequentially.
    [Fact]
    public async Task SpawnPipelineRuns_InsufficientCapacity_ReturnsQueuedDoesNotClaim()
    {
        var harness = new Harness(capacity: CapacityDecision.Deny("namespace at capacity for requests.cpu"));
        var project = BuildProject("p1", repos: new[] { "repo-only" });

        var result = await harness.Sut.ExecuteAsync(
            EmptyConfig, project, "fix-bug", Envelope("42"), Trigger(), CancellationToken.None);

        harness.CallCount.Should().Be(0, "a capacity-deferred run must not be claimed");
        result.ClaimResults.Should().ContainSingle()
            .Which.Outcome.Should().Be(ClaimOutcome.Queued);
    }

    [Fact]
    public async Task SpawnPipelineRuns_SufficientCapacity_ClaimsAsBefore()
    {
        var harness = new Harness(capacity: CapacityDecision.Admit());
        var project = BuildProject("p1", repos: new[] { "repo-only" });

        var result = await harness.Sut.ExecuteAsync(
            EmptyConfig, project, "fix-bug", Envelope("42"), Trigger(), CancellationToken.None);

        harness.CallCount.Should().Be(1);
        result.ClaimResults.Should().ContainSingle()
            .Which.Outcome.Should().Be(ClaimOutcome.Claimed);
    }

    // p0320b: admission probes the run's REAL footprint — the orchestrator pod plus
    // one sandbox per repo — not a single sandbox.
    [Fact]
    public async Task Spawn_ThreeRepoProject_ProbesFullFootprint()
    {
        var orchestratorSize = new ResourceLimits("100m", "500m", "128Mi", "256Mi");
        var harness = new Harness(orchestrator: orchestratorSize);
        var project = BuildProject("p1", repos: new[] { "repo-a", "repo-b", "repo-c" });

        await harness.Sut.ExecuteAsync(
            EmptyConfig, project, "fix-bug", Envelope("42"), Trigger(), CancellationToken.None);

        harness.ProbedFootprint.Should().NotBeNull();
        harness.ProbedFootprint!.Orchestrator.Should().BeSameAs(orchestratorSize);
        harness.ProbedFootprint.Sandboxes.Should().HaveCount(3);
    }

    private sealed class Harness
    {
        public SpawnPipelineRunsUseCase Sut { get; }
        public int CallCount { get; private set; }
        public ClaimRequest? LastRequest { get; private set; }
        public RunFootprint? ProbedFootprint { get; private set; }

        public Harness(CapacityDecision? capacity = null, ResourceLimits? orchestrator = null)
        {
            var claimService = new Mock<ITicketClaimService>();
            claimService.Setup(c => c.ClaimAsync(
                    It.IsAny<ClaimRequest>(),
                    It.IsAny<AgentSmithConfig>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ClaimRequest, AgentSmithConfig, CancellationToken>(
                    (r, _, _) => { CallCount++; LastRequest = r; })
                .ReturnsAsync(ClaimResult.Claimed());

            var resolver = new Mock<ISandboxResourceResolver>();
            resolver.Setup(r => r.Resolve(
                    It.IsAny<ResolvedProject>(), It.IsAny<string?>(), It.IsAny<ContextYamlStackResources?>()))
                .Returns(ResourceLimits.Default);

            var orchestratorResolver = new Mock<IOrchestratorResourceResolver>();
            orchestratorResolver.Setup(r => r.Resolve(It.IsAny<ResolvedProject>()))
                .Returns(orchestrator);

            var probe = new Mock<ISandboxCapacityProbe>();
            probe.Setup(p => p.HasCapacityAsync(It.IsAny<RunFootprint>(), It.IsAny<CancellationToken>()))
                .Callback<RunFootprint, CancellationToken>((f, _) => ProbedFootprint = f)
                .ReturnsAsync(capacity ?? CapacityDecision.Admit());

            Sut = new SpawnPipelineRunsUseCase(
                claimService.Object, resolver.Object, orchestratorResolver.Object, probe.Object,
                NullLogger<SpawnPipelineRunsUseCase>.Instance);
        }
    }
}
