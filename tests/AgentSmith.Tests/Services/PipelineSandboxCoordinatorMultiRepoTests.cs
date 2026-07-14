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

    // p0336b: context-level scoping drops a whole sandbox within a kept repo.
    [Fact]
    public async Task Coordinator_ScopedContexts_ProvisionsOnlyKeptContexts_DropsTheRest()
    {
        // Distinct languages → distinct toolchain groups → one sandbox each.
        var harness = new Harness().WithRepo("server").WithDiscoveries("server",
            new RemoteContextDiscovery("sdk8", "src/Api", "csharp"),
            new RemoteContextDiscovery("client", "src/Client", "typescript"),
            new RemoteContextDiscovery("encrypter", "src/Enc", "go"));
        harness.Pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<string>>>(
            ContextKeys.ScopedContexts,
            new Dictionary<string, IReadOnlyList<string>> { ["server"] = ["sdk8", "client"] });

        var result = await harness.Sut.EnsureSandboxesAsync(
            new ResolvedProject(), harness.Pipeline, CancellationToken.None);

        result.Should().HaveCount(2, "encrypter is dropped; sdk8 + client kept");
    }

    // p0336b escalation invariant: a repo ABSENT from the scoped map keeps all
    // its contexts, so a mid-run ensure_repo_sandbox target still provisions.
    [Fact]
    public async Task Coordinator_ScopedContexts_RepoAbsentFromMap_KeepsAllContexts()
    {
        var harness = new Harness().WithRepo("server").WithDiscoveries("server",
            new RemoteContextDiscovery("sdk8", "src/Api", "csharp"),
            new RemoteContextDiscovery("client", "src/Client", "typescript"));
        harness.Pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<string>>>(
            ContextKeys.ScopedContexts,
            new Dictionary<string, IReadOnlyList<string>> { ["other"] = ["x"] });

        var result = await harness.Sut.EnsureSandboxesAsync(
            new ResolvedProject(), harness.Pipeline, CancellationToken.None);

        result.Should().HaveCount(2, "server has no scoped entry → all its contexts kept");
    }

    // p0336c: same-image contexts of one repo collapse into ONE pod even when
    // their declared resources differ — sized to the max envelope. (Pre-p0336c
    // these were two pods, split by resource size.)
    [Fact]
    public async Task Coordinator_SameImageDifferentResources_CollapseToOnePod()
    {
        var harness = new Harness().WithRepo("server").WithDiscoveries("server",
            Csharp("api", memLimit: "3Gi"), Csharp("worker", memLimit: "4Gi"));
        harness.Pipeline.Set<string>(ContextKeys.PipelineName, "fix-bug"); // code pipeline honours context resources

        var result = await harness.Sut.EnsureSandboxesAsync(
            new ResolvedProject(), harness.Pipeline, CancellationToken.None);

        result.Should().ContainSingle("same toolchain image → one pod despite different declared resources");
        harness.Pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.SandboxContexts, out var contexts).Should().BeTrue();
        contexts!.Single().Value.Should().HaveCount(2, "both contexts share the one pod");
    }

    private static RemoteContextDiscovery Csharp(string context, string memLimit) =>
        new(context, "src/" + context, "csharp", Resources: new ContextYamlStackResources
        {
            CpuRequest = "250m", CpuLimit = "1", MemoryRequest = "1Gi", MemoryLimit = memLimit,
        });

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
        // p0180: one sandbox per toolchain group. p0322b: multi-group keys are
        // the speaking context names (unique per repo), not langSlugs. The
        // per-sandbox context list is recoverable via SandboxContexts.
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
        sandboxes.Keys.Should().BeEquivalentTo(new[] { "server", "client", "docs" });

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
