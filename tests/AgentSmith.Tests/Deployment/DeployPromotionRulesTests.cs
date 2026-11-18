using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Deployment;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Deployment;
using FluentAssertions;

namespace AgentSmith.Tests.Deployment;

/// <summary>2026-11-18-3863h: what promotion may do — only into environments the binding delivers without approval,
/// only from a deploy that settled green, at the version that settled, and never round a cycle.</summary>
public sealed class DeployPromotionRulesTests
{
    [Fact]
    public void PromotesTo_NamesOnlyPromotingEnvironmentsBoundWithoutApproval()
    {
        var declaration = new DeploymentDeclarationParser().Parse(DeployFixture.Declaration.Replace(
            "    promotes_from: test", "    promotes_from: test\n  - name: preview\n    manifest: m.yaml\n    version_key: v\n    promotes_from: test")).Declaration!;
        var binding = DeployFixture.Project().Templates[0] with
        {
            Environments = [.. DeployFixture.Project().Templates[0].Environments!, new DeploymentEnvironment("preview", null, false, new Dictionary<string, string>())],
        };
        var input = new DeployPlanInput(DeployFixture.Context("test", "v2"), new DeploymentTarget(binding, binding.Environments![0]), declaration, null);

        DeployDeliveredPublisher.PromotesTo(input).Should().Equal("preview"); // prod promotes from test too, but needs approval
    }

    [Theory]
    [InlineData("promotes_from: test", "promotes_from: prod")]
    [InlineData("    version_key: images.0.newTag\n  - name: prod", "    version_key: images.0.newTag\n    promotes_from: prod\n  - name: prod")]
    public void Parse_PromotionCycle_IsRefused(string from, string to)
    {
        var text = DeployFixture.Declaration.Replace("\n        ", "\n").Replace(from, to);

        new DeploymentDeclarationParser().Parse(text).Error.Should().Contain("promote in a cycle");
    }

    [Fact]
    public async Task Execute_PromotionAtTheSettledVersion_DeliversIt_ANewerSourceVersionIsNotPromoted()
    {
        var fixture = new DeployFixture(); // test's manifest carries v1

        var settled = await fixture.Handler().ExecuteAsync(Promoted("v1"), CancellationToken.None);
        var stale = await fixture.Handler().ExecuteAsync(Promoted("v0"), CancellationToken.None);

        settled.IsSuccess.Should().BeTrue(settled.Message);
        settled.Message.Should().Contain("v1 to prod");
        stale.IsSuccess.Should().BeFalse();
        stale.Message.Should().Contain("'test' now carries 'v1', not 'v0' that settled there");
    }

    [Theory]
    [InlineData(PrReviewStates.Merged, CiStates.Green, null, true)]
    [InlineData(PrReviewStates.Merged, CiStates.None, null, true)]
    [InlineData(PrReviewStates.Merged, CiStates.Red, null, false)]
    [InlineData(PrReviewStates.Closed, CiStates.Green, null, false)]
    [InlineData(null, CiStates.Green, "42", true)]
    [InlineData(null, CiStates.Red, "42", false)]
    public void Succeeded_IsAMergedGreenPullRequestOrAGreenPipeline(string? pr, string ci, string? run, bool expected) =>
        DeployFollowVerdict.Succeeded(new DeployRecord { PullRequestState = pr, CiState = ci, PipelineRunId = run, SettledAtTicks = 1 })
            .Should().Be(expected);

    private static DeployToEnvironmentContext Promoted(string version)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "20261118-0202-cd34");
        return new DeployToEnvironmentContext(pipeline, DeployFixture.Project(), new DeployRequest("api", null, "prod", null, version));
    }
}
