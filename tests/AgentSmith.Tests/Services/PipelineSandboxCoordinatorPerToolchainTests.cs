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
                new RemoteContextDiscovery("clientapigenerator", "src/ClientGenerator", "csharp"),
                new RemoteContextDiscovery("copyrheview", "src/CopyrhEview", "csharp"),
                new RemoteContextDiscovery("databasemigrator", "src/DatabaseMigrator", "csharp"),
                new RemoteContextDiscovery("treevalidator", "src/TreeValidator", "csharp"),
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
                new RemoteContextDiscovery("clientapigenerator", "src/ClientGenerator", "csharp"),
                new RemoteContextDiscovery("copyrheview", "src/CopyrhEview", "csharp"),
                new RemoteContextDiscovery("databasemigrator", "src/DatabaseMigrator", "csharp"),
                new RemoteContextDiscovery("treevalidator", "src/TreeValidator", "csharp"),
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
            .Should().BeEquivalentTo("api", "clientapigenerator", "copyrheview", "databasemigrator", "treevalidator");
    }

    [Fact]
    public async Task MixedToolchainsOnOneRepo_CreatesOneSandboxPerGroup()
    {
        _resolverMock.Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RemoteContextDiscovery("api", "src/Api", "csharp"),
                new RemoteContextDiscovery("clientapigenerator", "src/ClientGenerator", "csharp"),
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

    private PipelineSandboxCoordinator NewSut() => new(
        _factoryMock.Object,
        _specBuilder,
        _resolverMock.Object,
        AgentSmith.Tests.TestHelpers.EventTestStubs.NoOp,
        AgentSmith.Tests.TestHelpers.EventTestStubs.RunContext,
        new NoOpSandboxLivenessSupervisor(),
        NullLogger<PipelineSandboxCoordinator>.Instance);
}
