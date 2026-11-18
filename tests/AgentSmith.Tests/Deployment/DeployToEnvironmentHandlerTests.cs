using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Deployment;

/// <summary>2026-11-18-3863c: the deploy step, per the template's rules, against faked hosts.</summary>
public sealed class DeployToEnvironmentHandlerTests
{
    [Fact]
    public async Task Execute_EntryEnvironmentWithoutApproval_WritesOpensAndMerges()
    {
        var fixture = new DeployFixture();

        var result = await fixture.Handler().ExecuteAsync(DeployFixture.Context("test", "v2"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Message.Should().Contain("Deploy of `api` v2 to test").And.Contain("pull request https://stub.test/app/pull/1 merged")
            .And.Contain("CI is not readable on this host");
        fixture.App.Verify(p => p.WriteFilesToBranchAsync(
            new BranchName("agent-smith/deploy/api-test/20261118-0101-ab12"),
            It.Is<IReadOnlyList<RepoFile>>(f => f.Single().Path == "deploy/test/kustomization.yaml"
                && f.Single().Content == DeployFixture.Manifest("v2")),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_PromotedEnvironment_TakesTheSourceVersionAndWaitsForAPerson()
    {
        var fixture = new DeployFixture();

        var result = await fixture.Handler().ExecuteAsync(DeployFixture.Context("prod", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Message.Should().Contain("v1 to prod").And.Contain("waits for a person to merge it");
        fixture.Ops.Verify(p => p.MergePullRequestAsync(It.IsAny<string>(), It.IsAny<BranchName>(),
            It.IsAny<BranchProtection>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_TypedVersionForAPromotedEnvironment_IsRefused()
    {
        var result = await new DeployFixture().Handler().ExecuteAsync(DeployFixture.Context("prod", "v9"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("promotes from 'test'");
    }

    [Fact]
    public async Task Execute_EntryEnvironmentWithoutVersion_IsRefused() =>
        (await new DeployFixture().Handler().ExecuteAsync(DeployFixture.Context("test", null), CancellationToken.None))
            .Message.Should().Contain("entry environment; name the version");

    [Fact]
    public async Task Execute_VersionAlreadyThere_WritesNothing()
    {
        var fixture = new DeployFixture();

        var result = await fixture.Handler().ExecuteAsync(DeployFixture.Context("test", "v1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("already carries it");
        fixture.App.Verify(p => p.WriteFilesToBranchAsync(It.IsAny<BranchName>(), It.IsAny<IReadOnlyList<RepoFile>>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_MergeRefused_IsSaidAndTheRunSucceeds()
    {
        var fixture = new DeployFixture();
        fixture.App.Setup(p => p.MergePullRequestAsync(It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<BranchProtection>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(PullRequestCompletion.Refused("branch policy"));

        var result = await fixture.Handler().ExecuteAsync(DeployFixture.Context("test", "v2"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("the host refused the merge (branch policy)");
    }

    [Fact]
    public async Task Execute_UnboundEnvironmentAndMissingSeeds_AreRefused()
    {
        var handler = new DeployFixture().Handler();

        (await handler.ExecuteAsync(DeployFixture.Context("staging", "v2"), CancellationToken.None))
            .Message.Should().Contain("binds no environment 'staging'");
        (await handler.ExecuteAsync(new DeployToEnvironmentContext(new(), DeployFixture.Project(), null), CancellationToken.None))
            .Message.Should().Contain("ContextKeys.DeployContext");
    }
}
