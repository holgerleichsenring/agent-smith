using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Tests.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
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
        // 2026-11-25-9992b: both repositories share the image, so the one workspace holds one container for both.
        harness.Workspaces.Created.Should().ContainSingle();
        harness.Workspaces.Sandboxes.Spawned.Should().ContainSingle();
    }

    // 2026-11-25-9992b: two repositories with two images are ONE workspace with a toolchain per image; each
    // (repository, image) group is still its own key, served by its image's container.
    [Fact]
    public async Task Coordinator_TwoReposTwoImages_OneWorkspaceOneToolchainPerImage()
    {
        var harness = new Harness().WithRepo("api").WithRepo("web")
            .WithDiscoveries("api",
                new RemoteContextDiscovery("server", "src/Server", "csharp"),
                new RemoteContextDiscovery("client", "src/Client", "typescript"))
            .WithDiscoveries("web", new RemoteContextDiscovery("default", ".", "typescript"));

        var result = await harness.Sut.EnsureSandboxesAsync(new ResolvedProject(), harness.Pipeline, CancellationToken.None);

        var workspace = harness.Workspaces.Created.Should().ContainSingle().Subject;
        workspace.Spec.Toolchains.Select(t => t.Image).Should().OnlyHaveUniqueItems().And.HaveCount(2);
        result.Keys.Should().BeEquivalentTo("api-server", "api-client", "web");
        var repos = harness.Pipeline.Get<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos);
        repos.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["api-server"] = "api", ["api-client"] = "api", ["web"] = "web",
        });
        result["api-client"].Backend().Should().BeSameAs(result["web"].Backend(), "one image, one container");
        result["api-server"].Backend().Should().NotBeSameAs(result["api-client"].Backend());
    }

    // A repeated call with nothing new reuses every key and the workspace.
    [Fact]
    public async Task Coordinator_RepeatedEnsure_ReusesKeysAndWorkspace()
    {
        var harness = new Harness().WithRepo("api").WithRepo("web");

        var first = (await harness.Sut.EnsureSandboxesAsync(new ResolvedProject(), harness.Pipeline, CancellationToken.None))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        var second = await harness.Sut.EnsureSandboxesAsync(new ResolvedProject(), harness.Pipeline, CancellationToken.None);

        second.Should().BeEquivalentTo(first);
        harness.Workspaces.Created.Should().ContainSingle();
    }

    // A repository brought in with an image the workspace lacks goes through the restart: the workspace is created
    // again with every toolchain, and the keys handed out before reach the new containers.
    [Fact]
    public async Task Coordinator_NewImageOnASecondCall_RestartsTheWorkspaceWithEveryToolchain()
    {
        var harness = new Harness().WithRepo("api")
            .WithDiscoveries("api", new RemoteContextDiscovery("default", ".", "csharp"))
            .WithDiscoveries("web", new RemoteContextDiscovery("default", ".", "typescript"));
        var first = await harness.Sut.EnsureSandboxesAsync(new ResolvedProject(), harness.Pipeline, CancellationToken.None);
        var apiKey = first.Keys.Single();

        harness.WithRepo("web");
        var second = await harness.Sut.EnsureSandboxesAsync(new ResolvedProject(), harness.Pipeline, CancellationToken.None);

        harness.Workspaces.Created.Should().HaveCount(2);
        harness.Workspaces.Created[0].DisposeCount.Should().Be(1, "the old workspace is gone");
        harness.Workspaces.Created[1].Spec.Toolchains.Should().HaveCount(2);
        second.Keys.Should().HaveCount(2).And.Contain(apiKey);
        harness.Workspaces.Created[1].Containers.Should().Contain(c => ReferenceEquals(c, second[apiKey].Backend()));
        harness.Workspaces.Created[1].Containers.SelectMany(c => c.RanSteps).Should().Contain(
            s => s.Command == "git" && s.Args!.Contains("-B") && s.WorkingDirectory == "/work/api",
            "the repository the run had is checked out again at its saved commit");
    }

    // p0336c: same-image contexts of one repo collapse into ONE pod even when
    // their declared resources differ — sized to the max envelope. (Pre-p0336c
    // these were two pods, split by resource size.)
    [Fact]
    public async Task Coordinator_SameImageDifferentResources_CollapseToOnePod()
    {
        var harness = new Harness().WithRepo("server").WithDiscoveries("server",
            Csharp("api", memLimit: "3Gi"), Csharp("worker", memLimit: "4Gi"));
        harness.Pipeline.Set<string>(ContextKeys.PipelineName, "code"); // code pipeline honours context resources

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

        var workspace = harness.Workspaces.Created.Should().ContainSingle().Subject;
        workspace.DisposeCount.Should().Be(1);
        workspace.Containers.Should().NotBeEmpty().And.OnlyContain(c => c.Disposed);
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
            It.IsAny<RepoConnection>(), It.IsAny<ContextReadBranches>(), It.IsAny<CancellationToken>()),
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


        await harness.Sut.EnsureSandboxesAsync(
            new ResolvedProject(), harness.Pipeline, CancellationToken.None);

        harness.Pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, out var sandboxes).Should().BeTrue();
        sandboxes!.Should().HaveCount(3);
        sandboxes.Keys.Should().BeEquivalentTo(new[] { "server", "client", "docs" });

        harness.Workspaces.Sandboxes.Spawned.Select(s => s.Spec.ToolchainImage).Should().HaveCount(3)
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
        public StubWorkspaceFactory Workspaces { get; } = new(new StubSandboxFactory());
        public Mock<ISandboxLanguageResolver> LanguageResolverMock { get; } = new();
        public PipelineSandboxCoordinator Sut { get; }

        private readonly List<RepoConnection> _repos = new();

        public Harness()
        {
            LanguageResolverMock.Setup(r => r.ResolveAllAsync(
                    It.IsAny<RepoConnection>(), It.IsAny<ContextReadBranches>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new RemoteContextDiscovery("default", ".", null) });

            Sut = TestCoordinators.Create(Workspaces, LanguageResolverMock.Object,
                new SandboxSpecBuilder(
                    WiredResourceResolver.Create(),
                    new StubAgentImageResolver()));

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
                    It.Is<RepoConnection>(rc => rc.Name == repoName), It.IsAny<ContextReadBranches>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(discoveries);
            return this;
        }

        private sealed class StubAgentImageResolver : IAgentImageResolver
        {
            public string Resolve(ResolvedProject projectConfig) => "agent-smith-sandbox-agent:test";
        }
    }
}
