using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0180: PipelineSandboxCoordinator dedupes sandboxes by toolchain image
/// within a repo. The named target test (operator's regression marker) is
/// FiveCsharpContextsOnOneRepo_CreatesOneSandbox: five RemoteContextDiscovery
/// entries that all resolve to the dotnet image produce exactly ONE call to
/// ISandboxFactory.CreateAsync.
/// </summary>
public sealed class PipelineSandboxCoordinatorPerToolchainTests
{
    private readonly Mock<ISandboxFactory> _factoryMock = new();
    private readonly Mock<ISandboxLanguageResolver> _resolverMock = new();
    private readonly SandboxSpecBuilder _specBuilder
        = new(new StubSandboxResourceResolver(), new StubAgentImageResolver());

    [Fact]
    public async Task FiveCsharpContextsOnOneRepo_CreatesOneSandbox()
    {
        _resolverMock.Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RemoteContextDiscovery("api", "src/Api", "csharp"),
                new RemoteContextDiscovery("component-a", "src/ClientGenerator", "csharp"),
                new RemoteContextDiscovery("component-b", "src/component-b", "csharp"),
                new RemoteContextDiscovery("component-c", "src/component-c", "csharp"),
                new RemoteContextDiscovery("component-d", "src/component-d", "csharp"),
            });
        _factoryMock.Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ISandbox>().Object);

        var context = new PipelineContext();
        context.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            new[] { new RepoConnection { Name = "sample-server" } });

        var sut = NewSut();
        var sandboxes = await sut.EnsureSandboxesAsync(new ResolvedProject(), context, CancellationToken.None);

        _factoryMock.Verify(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()), Times.Once);
        sandboxes.Should().HaveCount(1);
    }

    [Fact]
    public async Task FiveCsharpContextsOnOneRepo_RegistersAllFiveContextsInSandboxContexts()
    {
        _resolverMock.Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RemoteContextDiscovery("api", "src/Api", "csharp"),
                new RemoteContextDiscovery("component-a", "src/ClientGenerator", "csharp"),
                new RemoteContextDiscovery("component-b", "src/component-b", "csharp"),
                new RemoteContextDiscovery("component-c", "src/component-c", "csharp"),
                new RemoteContextDiscovery("component-d", "src/component-d", "csharp"),
            });
        _factoryMock.Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ISandbox>().Object);

        var context = new PipelineContext();
        context.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            new[] { new RepoConnection { Name = "sample-server" } });

        var sut = NewSut();
        var sandboxes = await sut.EnsureSandboxesAsync(new ResolvedProject(), context, CancellationToken.None);

        var contexts = context.Get<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(ContextKeys.SandboxContexts);
        contexts.Should().HaveCount(1);
        var key = sandboxes.Keys.Single();
        contexts[key].Should().HaveCount(5);
        contexts[key].Select(d => d.ContextName)
            .Should().BeEquivalentTo("api", "component-a", "component-b", "component-c", "component-d");
    }

    [Fact]
    public async Task MixedToolchainsOnOneRepo_CreatesOneSandboxPerGroup()
    {
        _resolverMock.Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RemoteContextDiscovery("api", "src/Api", "csharp"),
                new RemoteContextDiscovery("component-a", "src/ClientGenerator", "csharp"),
                new RemoteContextDiscovery("frontend", "src/Frontend", "typescript"),
                new RemoteContextDiscovery("docs", "docs", null),
            });
        _factoryMock.Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ISandbox>().Object);

        var context = new PipelineContext();
        context.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            new[] { new RepoConnection { Name = "multi-stack-repo" } });

        var sut = NewSut();
        var sandboxes = await sut.EnsureSandboxesAsync(new ResolvedProject(), context, CancellationToken.None);

        // csharp (2 contexts) → 1 sandbox; typescript (1) → 1 sandbox; null/generic (1) → 1 sandbox.
        _factoryMock.Verify(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        sandboxes.Should().HaveCount(3);
    }

    [Fact]
    public async Task Coordinator_TwoImageGroups_DistinctSpeakingKeys_NoNumericBackstop()
    {
        // p0322b: same language + same resources but DIFFERENT toolchain images →
        // two groups. The old lang+size key hid the differing image and showed the
        // identical parts, colliding into the "-2" backstop ("csharp", "csharp-2").
        // Speaking keys carry each group's representative context name instead.
        _resolverMock.Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RemoteContextDiscovery("api", "src/Api", "csharp",
                    ToolchainImage: "mcr.microsoft.com/dotnet/sdk:9.0"),
                new RemoteContextDiscovery("legacy", "src/Legacy", "csharp",
                    ToolchainImage: "mcr.microsoft.com/dotnet/sdk:8.0"),
            });
        _factoryMock.Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ISandbox>().Object);

        var context = new PipelineContext();
        context.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            new[] { new RepoConnection { Name = "sample-server" } });

        var sandboxes = await NewSut().EnsureSandboxesAsync(new ResolvedProject(), context, CancellationToken.None);

        sandboxes.Keys.Should().BeEquivalentTo("api", "legacy");
        sandboxes.Keys.Should().NotContain(k => k.EndsWith("-2", StringComparison.Ordinal));
    }

    private PipelineSandboxCoordinator NewSut() => new(
        _factoryMock.Object,
        _specBuilder,
        _resolverMock.Object,
        AgentSmith.Tests.TestHelpers.EventTestStubs.NoOp,
        AgentSmith.Tests.TestHelpers.EventTestStubs.RunContext,
        new NoOpSandboxLivenessSupervisor(),
        NullLogger<PipelineSandboxCoordinator>.Instance);
}
