using AgentSmith.Application.Services.Spawning;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.EndToEnd;

/// <summary>
/// End-to-end smoke for the unified-run model: a single ticket envelope resolves
/// to a project with three repos and produces exactly ONE ClaimAsync call (no
/// per-repo fan-out). p0158 reversal of the p0140b fan-out behaviour.
/// </summary>
public sealed class MultiRepoEnqueueSmokeTests
{
    [Fact]
    public async Task MultiRepoEnqueueSmoke_ThreeRepos_OneTicket_EmitsExactlyOneClaimRequest_NotThree()
    {
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["multi"] = new()
                {
                    Name = "multi",
                    Repos = new[]
                    {
                        new RepoConnection { Name = "repo-a" },
                        new RepoConnection { Name = "repo-b" },
                        new RepoConnection { Name = "repo-c" }
                    },
                    GithubTrigger = new WebhookTriggerConfig
                    {
                        ProjectResolution = new ProjectResolutionConfig
                        {
                            Strategy = ResolutionStrategy.Tag, Value = "agent-smith"
                        },
                        DefaultPipeline = "fix-bug"
                    }
                }
            }
        };

        var calls = 0;
        ClaimRequest? captured = null;
        var claimService = new Mock<ITicketClaimService>();
        claimService.Setup(c => c.ClaimAsync(
                It.IsAny<ClaimRequest>(),
                It.IsAny<AgentSmithConfig>(),
                It.IsAny<CancellationToken>()))
            .Callback<ClaimRequest, AgentSmithConfig, CancellationToken>(
                (r, _, _) => { calls++; captured = r; })
            .ReturnsAsync(ClaimResult.Claimed());

        var resolver = new ProjectResolver(NullLogger<ProjectResolver>.Instance);
        var spawn = new SpawnPipelineRunsUseCase(
            claimService.Object, NullLogger<SpawnPipelineRunsUseCase>.Instance);

        var envelope = new IncomingTicketEnvelope
        {
            Labels = new[] { "agent-smith" },
            TicketId = "42",
            Platform = "github"
        };
        var matches = resolver.Resolve(config, envelope);

        matches.Should().HaveCount(1);
        var match = matches[0];
        var project = config.Projects[match.ProjectName];
        var trigger = project.GithubTrigger!;

        await spawn.ExecuteAsync(
            config, project, match.PipelineName, envelope, trigger, CancellationToken.None);

        calls.Should().Be(1);
        captured.Should().NotBeNull();
        captured!.Platform.Should().Be("github");
        captured.TicketId.Value.Should().Be("42");
        captured.PipelineName.Should().Be("fix-bug");
    }
}
