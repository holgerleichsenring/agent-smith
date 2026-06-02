using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0158e spec-driven multi-sandbox lifecycle: one ISandbox per configured
/// repo, per-repo toolchain resolution, dispose-all symmetry.
/// </summary>
public sealed class PipelineSandboxCoordinatorMultiRepoTests
{
    [Fact]
    public async Task Coordinator_PerRepoSandbox_OnePerConfigured()
    {
        var harness = new Harness().WithRepo("server").WithRepo("client");

        var result = await harness.Sut.EnsureSandboxesAsync(
            new ResolvedProject(), harness.Pipeline, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Keys.Should().BeEquivalentTo(new[] { "server", "client" });
        harness.FactoryMock.Verify(f => f.CreateAsync(
            It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Coordinator_DisposeAsync_DisposesEverySandboxOnce()
    {
        var harness = new Harness().WithRepo("a").WithRepo("b").WithRepo("c");

        await harness.Sut.EnsureSandboxesAsync(
            new ResolvedProject(), harness.Pipeline, CancellationToken.None);
        await harness.Sut.DisposeAsync();
        await harness.Sut.DisposeAsync();

        foreach (var sandbox in harness.SandboxMocks)
            sandbox.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task Coordinator_ToolchainResolution_PerRepo_NotAggregate()
    {
        var harness = new Harness().WithRepo("server").WithRepo("client");

        await harness.Sut.EnsureSandboxesAsync(
            new ResolvedProject(), harness.Pipeline, CancellationToken.None);

        // ISandboxLanguageResolver.ResolveAllAsync called once per repo (per-repo
        // discovery, not a single aggregate call).
        harness.LanguageResolverMock.Verify(r => r.ResolveAllAsync(
            It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Coordinator_PublishesSandboxesAndLegacySingularSandbox()
    {
        var harness = new Harness().WithRepo("primary").WithRepo("sibling");

        await harness.Sut.EnsureSandboxesAsync(
            new ResolvedProject(), harness.Pipeline, CancellationToken.None);

        harness.Pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, out var sandboxes).Should().BeTrue();
        sandboxes!.Should().HaveCount(2);
        harness.Pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var primary).Should().BeTrue();
        primary.Should().BeSameAs(sandboxes["primary"]);
    }

    [Fact]
    public async Task Coordinator_SingleDefaultContext_SpawnsOneSandbox_KeyIsDefaultName()
    {
        var harness = new Harness().WithRepo("agent-smith");

        await harness.Sut.EnsureSandboxesAsync(
            new ResolvedProject(), harness.Pipeline, CancellationToken.None);

        harness.Pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, out var sandboxes).Should().BeTrue();
        sandboxes!.Keys.Should().BeEquivalentTo(new[] { "default" });
    }

    [Fact]
    public async Task Coordinator_UninitRepo_FallsBackToOneRootSandbox()
    {
        var harness = new Harness().WithRepo("uninit");
        // Resolver's synthetic-default discovery already exercises this path:
        // empty ListDirectoryAsync → one ("default", ".", null). Default mock
        // returns the synthetic, so the coordinator should spawn one sandbox
        // keyed "default" with no language → generic fallback image.
        await harness.Sut.EnsureSandboxesAsync(
            new ResolvedProject(), harness.Pipeline, CancellationToken.None);

        harness.Pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, out var sandboxes).Should().BeTrue();
        sandboxes!.Should().ContainSingle().Which.Key.Should().Be("default");
        harness.Pipeline.TryGet<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries, out var discoveries).Should().BeTrue();
        discoveries!["default"].Language.Should().BeNull();
    }

    [Fact]
    public async Task Coordinator_MonorepoThreeContexts_SpawnsThreeSandboxes_OneToolchainEach()
    {
        // p0180: keys now distinguish by langSlug (not context name) within a
        // single repo. Three distinct toolchains → three sandboxes with langSlug
        // keys. The per-sandbox context list is recoverable via SandboxContexts.
        var harness = new Harness().WithRepo("monorepo")
            .WithDiscoveries("monorepo",
                new RemoteContextDiscovery("server", "src/Server", "csharp"),
                new RemoteContextDiscovery("client", "src/Client", "typescript"),
                new RemoteContextDiscovery("docs", "docs", "markdown"));

        var captured = new List<SandboxSpec>();
        harness.FactoryMock.Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .Callback<SandboxSpec, CancellationToken>((spec, _) => captured.Add(spec))
            .ReturnsAsync(() => new Mock<ISandbox>().Object);

        await harness.Sut.EnsureSandboxesAsync(
            new ResolvedProject(), harness.Pipeline, CancellationToken.None);

        harness.Pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, out var sandboxes).Should().BeTrue();
        sandboxes!.Should().HaveCount(3);
        sandboxes.Keys.Should().BeEquivalentTo(new[] { "csharp", "typescript", "markdown" });

        captured.Select(s => s.ToolchainImage).Should().HaveCount(3)
            .And.OnlyHaveUniqueItems("each context should map to its own toolchain image");
    }

    [Fact]
    public async Task Coordinator_MultiRepoMonorepoMix_ComposesPerToolchainKeys()
    {
        // p0180: backend's 2 csharp contexts share ONE sandbox (was 2 in
        // p0161a). frontend (single typescript context) gets its own.
        var harness = new Harness().WithRepo("frontend").WithRepo("backend")
            .WithDiscoveries("frontend",
                new RemoteContextDiscovery("default", ".", "typescript"))
            .WithDiscoveries("backend",
                new RemoteContextDiscovery("api", "src/Api", "csharp"),
                new RemoteContextDiscovery("worker", "src/Worker", "csharp"));

        await harness.Sut.EnsureSandboxesAsync(
            new ResolvedProject(), harness.Pipeline, CancellationToken.None);

        harness.Pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, out var sandboxes).Should().BeTrue();
        sandboxes!.Keys.Should().BeEquivalentTo(new[]
        {
            "frontend",   // multi-repo single-toolchain → bare repo name
            "backend",    // multi-repo single-toolchain (2 csharp contexts collapsed)
        });

        harness.Pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.SandboxContexts, out var contexts).Should().BeTrue();
        contexts!["backend"].Should().HaveCount(2);
        contexts["backend"].Select(d => d.ContextName).Should().BeEquivalentTo("api", "worker");
    }

    private sealed class Harness
    {
        public PipelineContext Pipeline { get; } = new();
        public Mock<ISandboxFactory> FactoryMock { get; } = new();
        public Mock<ISandboxLanguageResolver> LanguageResolverMock { get; } = new();
        public List<Mock<ISandbox>> SandboxMocks { get; } = new();
        public PipelineSandboxCoordinator Sut { get; }

        private readonly List<RepoConnection> _repos = new();

        public Harness()
        {
            FactoryMock.Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    var sandbox = new Mock<ISandbox>();
                    SandboxMocks.Add(sandbox);
                    return Task.FromResult(sandbox.Object);
                });
            LanguageResolverMock.Setup(r => r.ResolveAllAsync(
                    It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new RemoteContextDiscovery("default", ".", null) });

            Sut = new PipelineSandboxCoordinator(
                FactoryMock.Object,
                new SandboxSpecBuilder(
                    new SandboxResourceResolver(Options.Create(new SandboxOptions())),
                    new StubAgentImageResolver()),
                LanguageResolverMock.Object,
                AgentSmith.Tests.TestHelpers.EventTestStubs.NoOp,
                AgentSmith.Tests.TestHelpers.EventTestStubs.RunContext,
                new NoOpSandboxLivenessSupervisor(),
                NullLogger<PipelineSandboxCoordinator>.Instance);

            Pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, _repos);
        }

        public Harness WithRepo(string name)
        {
            _repos.Add(new RepoConnection { Name = name });
            return this;
        }

        public Harness WithDiscoveries(string repoName, params RemoteContextDiscovery[] discoveries)
        {
            LanguageResolverMock.Setup(r => r.ResolveAllAsync(
                    It.Is<RepoConnection>(rc => rc.Name == repoName), It.IsAny<CancellationToken>()))
                .ReturnsAsync(discoveries);
            return this;
        }

        private sealed class StubAgentImageResolver : IAgentImageResolver
        {
            public string Resolve(ResolvedProject projectConfig) => "agent-smith-sandbox-agent:test";
        }
    }
}
