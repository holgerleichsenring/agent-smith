using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-08-25-014d: an image without git is no longer guessed at from its tag and
/// quietly swapped for another one. It is used, and it fails at the one step that
/// needs git — saying so.
/// </summary>
public sealed class ImageWithoutGitTests
{
    [Fact]
    public async Task Image_WithoutGit_FailsAtTheCheckoutWithANamedError()
    {
        var checkout = await CloneInto(new GitlessSandbox());

        checkout.Repository.Should().BeNull();
        checkout.Problem.Should().Contain("no git on PATH")
            .And.Contain("cloned INSIDE")
            .And.Contain("stack.image",
                "the operator has to be told which field to change, not just that a clone failed");
    }

    [Fact]
    public async Task Checkout_FailingForAnyOtherReason_IsNotBlamedOnTheImage()
    {
        var checkout = await CloneInto(new FailingSandbox(128, "fatal: repository not found"));

        checkout.Problem.Should().Contain("repository not found").And.NotContain("no git on PATH");
    }

    [Fact]
    public void Image_WithoutGit_IsNoLongerSilentlyDowngraded()
    {
        // The model named an alpine image. Before this phase the chain answered with a
        // DIFFERENT image and logged a warning nobody reads; the run then succeeded in a
        // toolchain the repository never asked for. Now the named image is the one used.
        new SandboxImageChain()
            .Resolve(new ResolvedProject(), "node", contextImage: "node:20-alpine")
            .Should().Be("node:20-alpine");
    }

    [Fact]
    public void Image_OfAnUnknownEcosystem_IsAcceptedWhenItsRegistryIs()
    {
        // No table row, no tag this repository has ever heard of — and no reason to
        // refuse it: the registry is inside the boundary, which is the whole question.
        new SandboxImageChain()
            .Resolve(new ResolvedProject(), "elixir", contextImage: "ghcr.io/an-org/elixir:1.16-otp26")
            .Should().Be("ghcr.io/an-org/elixir:1.16-otp26");
    }

    private static async Task<RepoCheckout> CloneInto(ISandbox sandbox)
    {
        var provider = new Mock<ISourceProvider>();
        provider.SetupGet(p => p.ProviderType).Returns("GitHub");
        provider.Setup(p => p.CheckoutAsync(It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository(new BranchName("main"), "https://example.invalid/org/repo.git"));
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);

        var cloner = new SandboxRepoCloner(
            factory.Object,
            new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance),
            TestHelpers.TestGit.WorkBranchCheckout,
            NullLogger<SandboxRepoCloner>.Instance);

        return await cloner.CheckoutIntoSandboxesAsync(
            new RepoConnection { Name = "repo", Type = RepoType.GitHub, Url = "https://example.invalid/org/repo.git" },
            branch: null,
            [new KeyValuePair<string, ISandbox>("repo", sandbox)],
            CancellationToken.None);
    }

    // What the sandbox agent's ProcessRunner really reports when the binary is absent:
    // Process.Start throws, so there is no exit code, only the start failure.
    private sealed class GitlessSandbox : FailingSandbox
    {
        public GitlessSandbox()
            : base(-1, "failed to start 'git': An error occurred trying to start process "
                       + "'git' with working directory '/work'. No such file or directory") { }
    }

    private class FailingSandbox(int exitCode, string error) : ISandbox
    {
        public string JobId => "gitless";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct) =>
            Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, exitCode,
                TimedOut: false, DurationSeconds: 0.01, ErrorMessage: error));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
