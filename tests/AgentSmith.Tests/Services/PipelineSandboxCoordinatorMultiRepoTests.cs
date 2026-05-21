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

        // ISandboxLanguageResolver.ResolveAsync called once per repo (per-repo
        // resolution, not a single aggregate call).
        harness.LanguageResolverMock.Verify(r => r.ResolveAsync(
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
            LanguageResolverMock.Setup(r => r.ResolveAsync(
                    It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ToolchainResolutionResult(null, SandboxToolchainResolutionLayer.GenericFallback));

            Sut = new PipelineSandboxCoordinator(
                FactoryMock.Object,
                new SandboxSpecBuilder(
                    new SandboxResourceResolver(Options.Create(new SandboxOptions())),
                    new StubAgentImageResolver()),
                LanguageResolverMock.Object,
                NullLogger<PipelineSandboxCoordinator>.Instance);

            Pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, _repos);
        }

        public Harness WithRepo(string name)
        {
            _repos.Add(new RepoConnection { Name = name });
            return this;
        }

        private sealed class StubAgentImageResolver : IAgentImageResolver
        {
            public string Resolve(ResolvedProject projectConfig) => "agent-smith-sandbox-agent:test";
        }
    }
}
