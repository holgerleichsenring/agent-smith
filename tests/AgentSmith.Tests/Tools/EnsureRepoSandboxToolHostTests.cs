using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// p0331: the ensure_repo_sandbox escalation valve. Uses the REAL
/// PipelineSandboxCoordinator (the instance the executor owns, threaded via
/// ContextKeys.SandboxCoordinator) so idempotency and the by-reference sandbox
/// dict growth are exercised, not mocked away.
/// </summary>
public sealed class EnsureRepoSandboxToolHostTests
{
    private readonly Mock<ISandboxFactory> _factoryMock = new();
    private readonly Mock<ISandboxLanguageResolver> _resolverMock = new();
    private readonly Mock<ISourceProviderFactory> _sourceFactoryMock = new();
    private readonly SandboxSpecBuilder _specBuilder =
        new(new StubSandboxResourceResolver(), new StubAgentImageResolver());

    public EnsureRepoSandboxToolHostTests()
    {
        _factoryMock.Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var sandbox = new Mock<ISandbox>();
                sandbox.Setup(s => s.RunStepAsync(
                        It.IsAny<AgentSmith.Sandbox.Wire.Step>(),
                        It.IsAny<IProgress<AgentSmith.Sandbox.Wire.StepEvent>?>(),
                        It.IsAny<CancellationToken>()))
                    .Returns<AgentSmith.Sandbox.Wire.Step, IProgress<AgentSmith.Sandbox.Wire.StepEvent>?, CancellationToken>(
                        (step, _, _) => Task.FromResult(new AgentSmith.Sandbox.Wire.StepResult(
                            AgentSmith.Sandbox.Wire.StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null)));
                return sandbox.Object;
            });
        _resolverMock.Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new RemoteContextDiscovery("default", ".", "csharp") });
        // "Local" provider — checkout trusts the bind-mount, no in-sandbox clone.
        var provider = new Mock<ISourceProvider>();
        provider.SetupGet(p => p.ProviderType).Returns("Local");
        provider.Setup(p => p.CheckoutAsync(It.IsAny<BranchName?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository(new BranchName("main"), "git://stub"));
        _sourceFactoryMock.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);
    }

    [Fact]
    public async Task EnsureRepoSandbox_MidRun_SpawnsIdempotently_AndToolSeesIt()
    {
        var (pipeline, _, fs) = await BootRunAsync();
        var sut = Host(pipeline, fs, new UnboundedCapacityProbe());

        // Before the escalation the master cannot address the descoped repo.
        (await fs.RunCommand("echo hi", repo: "client")).Should().StartWith("Error: unknown repo");

        var first = await sut.EnsureRepoSandbox("client");
        var second = await sut.EnsureRepoSandbox("client");

        first.Should().Contain("Sandbox ready for repo 'client'");
        second.Should().Contain("already available");
        // Exactly ONE extra spawn across both calls (initial run spawned 1).
        _factoryMock.Verify(
            f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        // The scope seam was widened…
        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos)
            .Select(r => r.Name).Should().BeEquivalentTo("server", "client");
        // …the coordinator's by-reference dict grew…
        pipeline.Get<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes)
            .Should().HaveCount(2);
        // …and the RUNNING master's tool host sees the new repo (the p0331 gap:
        // FilesystemToolHost froze a readonly copy at construction).
        (await fs.RunCommand("echo hi", repo: "client")).Should().NotStartWith("Error");
    }

    [Fact]
    public async Task EnsureRepoSandbox_NoCapacity_HonestDeny_RunContinues()
    {
        var (pipeline, _, fs) = await BootRunAsync();
        var probe = new Mock<ISandboxCapacityProbe>();
        probe.Setup(p => p.HasCapacityAsync(It.IsAny<RunFootprint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CapacityDecision.Deny("namespace memory quota exhausted"));
        var sut = Host(pipeline, fs, probe.Object);

        var answer = await sut.EnsureRepoSandbox("client");

        // The deny is a readable tool ANSWER, not a crash — the run continues.
        answer.Should().Be(EnsureRepoSandboxToolHost.CapacityDenyAnswer);
        // Nothing was spawned and the scope seam is untouched.
        _factoryMock.Verify(
            f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()), Times.Once);
        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos)
            .Should().ContainSingle(r => r.Name == "server");
        // The single-sandbox footprint was probed (own probe, no orchestrator pod).
        probe.Verify(p => p.HasCapacityAsync(
            It.Is<RunFootprint>(f => f.Orchestrator == null && f.Sandboxes.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureRepoSandbox_UnknownRepo_NamesConfiguredRepos()
    {
        var (pipeline, _, fs) = await BootRunAsync();
        var sut = Host(pipeline, fs, new UnboundedCapacityProbe());

        var answer = await sut.EnsureRepoSandbox("ghost");

        answer.Should().StartWith("Error:").And.Contain("server").And.Contain("client");
        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos).Should().HaveCount(1);
    }

    // Boots a run the way PipelineExecutor does: project has TWO repos, the run
    // was narrowed to ONE (ScopeRepos), the real coordinator spawned its sandbox
    // and published itself + the project into the context; the master's tool
    // host was built from the then-current sandbox dict.
    private async Task<(PipelineContext Pipeline, ResolvedProject Project, FilesystemToolHost Fs)>
        BootRunAsync()
    {
        var server = new RepoConnection { Name = "server", Type = RepoType.Local, Path = "/tmp" };
        var client = new RepoConnection { Name = "client", Type = RepoType.Local, Path = "/tmp" };
        var project = new ResolvedProject { Name = "p", Repos = [server, client] };
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, new[] { server });
        pipeline.Set(ContextKeys.PipelineName, "fix-bug");

        var coordinator = new PipelineSandboxCoordinator(
            _factoryMock.Object, _specBuilder, _resolverMock.Object,
            EventTestStubs.NoOp, EventTestStubs.RunContext,
            new NoOpSandboxLivenessSupervisor(),
            NullLogger<PipelineSandboxCoordinator>.Instance);
        await coordinator.EnsureSandboxesAsync(project, pipeline, CancellationToken.None);

        var sandboxes = pipeline.Get<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes);
        var keyToRepo = pipeline.Get<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos);
        var fs = new FilesystemToolHost(sandboxes, sandboxes.Keys.First(), keyToRepo: keyToRepo);
        return (pipeline, project, fs);
    }

    private EnsureRepoSandboxToolHost Host(
        PipelineContext pipeline, FilesystemToolHost fs, ISandboxCapacityProbe probe) =>
        new(pipeline, fs, probe, new StubSandboxResourceResolver(),
            new SandboxRepoCloner(_sourceFactoryMock.Object, NullLogger<SandboxRepoCloner>.Instance),
            NullLogger<EnsureRepoSandboxToolHost>.Instance);
}
