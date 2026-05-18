using AgentSmith.Application.Services.Spawning;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Spawning;

/// <summary>
/// p0140b: SpawnPipelineRunsUseCase builds one ClaimRequest per repo on the project and
/// submits the batch through ITicketClaimService.ClaimSpawnAsync. These tests verify the
/// fan-out shape and per-request fields without exercising claim-region internals (those
/// are covered by ClaimSpawnAsyncTests).
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
        harness.LastRequests.Should().HaveCount(1);
        harness.LastRequests![0].RepoName.Should().Be("repo-only");
        harness.LastRequests[0].ProjectName.Should().Be("p1");
        harness.LastRequests[0].PipelineName.Should().Be("fix-bug");
    }

    [Fact]
    public async Task SpawnPipelineRuns_ThreeRepos_EnqueuesThreeRunsInOneClaimRegion()
    {
        var harness = new Harness();
        var project = BuildProject("p1", repos: new[] { "repo-a", "repo-b", "repo-c" });

        await harness.Sut.ExecuteAsync(
            EmptyConfig, project, "fix-bug", Envelope("42"), Trigger(), CancellationToken.None);

        harness.CallCount.Should().Be(1); // single ClaimSpawnAsync call holds the whole batch
        harness.LastRequests.Should().HaveCount(3);
        harness.LastRequests!.Select(r => r.RepoName)
            .Should().BeEquivalentTo(new[] { "repo-a", "repo-b", "repo-c" });
        harness.LastRequests.Should().AllSatisfy(r => r.TicketId.Value.Should().Be("42"));
        harness.LastRequests.Should().AllSatisfy(r => r.PipelineName.Should().Be("fix-bug"));
    }

    [Fact]
    public async Task SpawnPipelineRuns_PipelineWithAgentOverride_RequestCarriesPipelineNameUnchanged()
    {
        // The pipelineName parameter flows through unchanged. Per-pipeline Agent overrides
        // on the project are applied later by PipelineConfigResolver at consumer time;
        // spawn does not rewrite the pipeline name.
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

        harness.LastRequests.Should().NotBeNull();
        harness.LastRequests![0].PipelineName.Should().Be("fix-bug");
    }

    [Fact]
    public async Task SpawnPipelineRuns_InitialContextCarriesDoneStatusFromMatchedTrigger()
    {
        var harness = new Harness();
        var project = BuildProject("p1", repos: new[] { "repo-a" });
        var trigger = new WebhookTriggerConfig { DoneStatus = "In Review" };

        await harness.Sut.ExecuteAsync(
            EmptyConfig, project, "fix-bug", Envelope("42"), trigger, CancellationToken.None);

        harness.LastRequests.Should().NotBeNull();
        harness.LastRequests![0].InitialContext.Should().NotBeNull();
        harness.LastRequests[0].InitialContext![ContextKeys.DoneStatus].Should().Be("In Review");
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

    private sealed class Harness
    {
        public SpawnPipelineRunsUseCase Sut { get; }
        public int CallCount { get; private set; }
        public IReadOnlyList<ClaimRequest>? LastRequests { get; private set; }

        public Harness()
        {
            var claimService = new Mock<ITicketClaimService>();
            claimService.Setup(c => c.ClaimSpawnAsync(
                    It.IsAny<IReadOnlyList<ClaimRequest>>(),
                    It.IsAny<AgentSmithConfig>(),
                    It.IsAny<CancellationToken>()))
                .Callback<IReadOnlyList<ClaimRequest>, AgentSmithConfig, CancellationToken>(
                    (r, _, _) => { CallCount++; LastRequests = r; })
                .ReturnsAsync(Array.Empty<ClaimResult>());

            Sut = new SpawnPipelineRunsUseCase(
                claimService.Object, NullLogger<SpawnPipelineRunsUseCase>.Instance);
        }
    }
}
