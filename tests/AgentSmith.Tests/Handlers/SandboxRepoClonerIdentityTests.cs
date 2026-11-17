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
            new RunBranch(new BranchName("feature/x"), ComposedFromTicket: true),
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

    // 2026-11-17-d5cc: a bind-mounted checkout is never cloned over.
    [Fact]
    public async Task Checkout_LocalBindMount_IsNotCloned()
    {
        var sandbox = new RecordingSandbox();

        var checkout = await Cloner("Local").CheckoutIntoSandboxesAsync(
            new RepoConnection { Name = "local", Type = RepoType.Local, Path = "/tmp/repo", Url = "file:///tmp/repo" },
            new RunBranch(new BranchName("feature/x"), ComposedFromTicket: true),
            [new KeyValuePair<string, ISandbox>("local", sandbox)], CancellationToken.None);

        checkout.Problem.Should().BeNull();
        sandbox.RanSteps.Should().NotContain(s => s.Args!.Contains("clone"));
    }

    // 2026-11-17-d5cc: the in-process sandbox's fresh work dir holds no checkout, so the local repo is cloned
    // from its url and the run's branch is switched to, as for a remote repo.
    [Fact]
    public async Task Checkout_LocalRepoInEmptySandbox_ClonesFromItsUrlAndSwitchesBranch()
    {
        var sandbox = new RecordingSandbox { NoCheckout = true };

        var checkout = await Cloner("Local").CheckoutIntoSandboxesAsync(
            new RepoConnection { Name = "local", Type = RepoType.Local, Path = "/tmp/repo", Url = "file:///tmp/origin" },
            new RunBranch(new BranchName("feature/x"), ComposedFromTicket: true),
            [new KeyValuePair<string, ISandbox>("local", sandbox)], CancellationToken.None);

        checkout.Problem.Should().BeNull();
        sandbox.RanSteps.Should().Contain(s => s.Args!.Contains("clone") && s.Args!.Contains("file:///tmp/origin"));
        sandbox.RanSteps.Should().Contain(s => s.Args!.Contains("checkout") && s.Args!.Contains("feature/x"));
    }

    [Fact]
    public async Task Checkout_LocalRepoInEmptySandboxWithoutUrl_ClonesFromItsPath()
    {
        var sandbox = new RecordingSandbox { NoCheckout = true };

        await Cloner("Local").CheckoutIntoSandboxesAsync(
            new RepoConnection { Name = "local", Type = RepoType.Local, Path = "/tmp/repo" },
            branch: null, [new KeyValuePair<string, ISandbox>("local", sandbox)], CancellationToken.None);

        sandbox.RanSteps.Should().Contain(s => s.Args!.Contains("clone") && s.Args!.Contains("/tmp/repo"));
    }

    // 2026-10-02-5f89g: a missing secret refuses the checkout by name — no clone, no git prompt.
    [Fact]
    public async Task SandboxRepoCloner_RepoWithMissingSecret_FailsNamingTheSecret()
    {
        var sandbox = new RecordingSandbox();
        var cloner = Cloner("GitHub", new AgentSmith.Contracts.Services.GitTokenResolver(
            AgentSmith.Tests.TestSupport.TestCredentials.With()));

        var checkout = await cloner.CheckoutIntoSandboxesAsync(
            new RepoConnection { Name = "server", Type = RepoType.GitHub, Url = "https://host/org/repo.git", Auth = "absent_secret" },
            branch: null, [new KeyValuePair<string, ISandbox>("server", sandbox)], CancellationToken.None);

        checkout.Problem.Should().Contain("'absent_secret'");
        sandbox.RanSteps.Should().NotContain(s => s.Args!.Contains("clone"));
    }

    private static SandboxRepoCloner Cloner(
        string providerType, AgentSmith.Contracts.Services.IGitTokenResolver? credentials = null)
    {
        var provider = new Mock<ISourceProvider>();
        provider.SetupGet(p => p.ProviderType).Returns(providerType);
        provider.Setup(p => p.CheckoutAsync(It.IsAny<BranchName?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository(new BranchName("feature/x"), "https://host/org/repo.git"));
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);
        return new SandboxRepoCloner(
            factory.Object, credentials ?? AgentSmith.Tests.TestSupport.TestGitCredentials.Resolver,
            new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance),
            AgentSmith.Tests.TestHelpers.TestGit.WorkBranchCheckout, NullLogger<SandboxRepoCloner>.Instance);
    }

    private sealed class RecordingSandbox : ISandbox
    {
        public string JobId => "checkout-test";
        public List<Step> RanSteps { get; } = new();
        public bool NoCheckout { get; init; }

        public Task<StepResult> RunStepAsync(
            Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            RanSteps.Add(step);
            var outside = NoCheckout && step.Args!.Contains("--show-prefix");
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, ExitCode: outside ? 128 : 0,
                TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null, OutputContent: string.Empty));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
