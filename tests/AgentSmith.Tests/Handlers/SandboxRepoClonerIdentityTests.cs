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

namespace AgentSmith.Tests.Handlers;

// p0411: checkout is where the sandbox gets its committing identity — before the
// master is asked anything, so the master never spends a model call setting it.
public sealed class SandboxRepoClonerIdentityTests
{
    [Fact]
    public async Task Checkout_ConfiguresGitIdentity_BeforeTheMasterRuns()
    {
        var sandbox = new RecordingSandbox();
        var cloner = Cloner("GitHub");

        await cloner.CheckoutIntoSandboxesAsync(
            new RepoConnection { Name = "server", Type = RepoType.GitHub, Url = "https://host/org/repo.git" },
            new BranchName("feature/x"),
            [new KeyValuePair<string, ISandbox>("server", sandbox)],
            CancellationToken.None);

        var identitySteps = sandbox.RanSteps
            .Where(s => s.Command == "git" && s.Args!.Contains("config")).ToList();
        identitySteps.Should().Contain(s => s.Args!.Contains("user.email"));
        identitySteps.Should().Contain(s => s.Args!.Contains("user.name"));
    }

    [Fact]
    public async Task Checkout_LocalBindMount_StillEnsuresAnIdentity()
    {
        // A local repo skips the clone, but its sandbox must still be able to commit.
        var sandbox = new RecordingSandbox();

        await Cloner("Local").CheckoutIntoSandboxesAsync(
            new RepoConnection { Name = "local", Type = RepoType.Local, Path = "/tmp/repo" },
            branch: null,
            [new KeyValuePair<string, ISandbox>("local", sandbox)],
            CancellationToken.None);

        sandbox.RanSteps.Should().Contain(s => s.Args!.Contains("--get") && s.Args!.Contains("user.email"));
    }

    private static SandboxRepoCloner Cloner(string providerType)
    {
        var provider = new Mock<ISourceProvider>();
        provider.SetupGet(p => p.ProviderType).Returns(providerType);
        provider.Setup(p => p.CheckoutAsync(It.IsAny<BranchName?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository(new BranchName("feature/x"), "https://host/org/repo.git"));
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);
        return new SandboxRepoCloner(
            factory.Object,
            new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance),
            NullLogger<SandboxRepoCloner>.Instance);
    }

    private sealed class RecordingSandbox : ISandbox
    {
        public string JobId => "checkout-test";
        public List<Step> RanSteps { get; } = new();

        public Task<StepResult> RunStepAsync(
            Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            RanSteps.Add(step);
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0,
                TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null, OutputContent: string.Empty));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
