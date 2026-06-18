using AgentSmith.Application.Models;
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
using Microsoft.Extensions.Options;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0268: per-stack sandbox sizing. Two contexts on the SAME toolchain image but
/// with different context.yaml stack.resources must run in SEPARATE sandboxes
/// (a pod has one resource spec) with distinct, size-bearing keys — never silently
/// collapsed into one. Uses the REAL SandboxResourceResolver so context.yaml
/// resources actually flow into the resolved SandboxSpec.Resources.
/// </summary>
public sealed class PipelineSandboxCoordinatorPerStackSizeTests
{
    private readonly Mock<ISandboxFactory> _factoryMock = new();
    private readonly Mock<ISandboxLanguageResolver> _resolverMock = new();
    private readonly SandboxSpecBuilder _specBuilder = new(
        new SandboxResourceResolver(Options.Create(new SandboxOptions())),
        new StubAgentImageResolver());

    private static readonly ContextYamlStackResources Heavy = new("500m", "2", "1Gi", "4Gi");
    private static readonly ContextYamlStackResources Light = new("100m", "500m", "256Mi", "512Mi");

    [Fact]
    public async Task SameImageDifferentResources_SplitsSandboxes()
    {
        _resolverMock.Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RemoteContextDiscovery("build", "src/Api", "csharp", Resources: Heavy),
                new RemoteContextDiscovery("scan", "src/Api", "csharp", Resources: Light),
            });
        _factoryMock.Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ISandbox>().Object);

        var context = NewContext("sample-server");
        var sandboxes = await NewSut().EnsureSandboxesAsync(new ResolvedProject(), context, CancellationToken.None);

        // Same image (sdk:9.0), different size → two pods.
        _factoryMock.Verify(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        sandboxes.Should().HaveCount(2);
        sandboxes.Keys.Should().BeEquivalentTo("csharp-2-4gi", "csharp-500m-512mi");
    }

    [Fact]
    public async Task SameImageSameResources_CollapsesToOneSandbox()
    {
        _resolverMock.Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RemoteContextDiscovery("a", "src/A", "csharp", Resources: Heavy),
                new RemoteContextDiscovery("b", "src/B", "csharp", Resources: Heavy),
            });
        _factoryMock.Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ISandbox>().Object);

        var context = NewContext("sample-server");
        var sandboxes = await NewSut().EnsureSandboxesAsync(new ResolvedProject(), context, CancellationToken.None);

        _factoryMock.Verify(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()), Times.Once);
        sandboxes.Should().HaveCount(1);
    }

    private static PipelineContext NewContext(string repoName)
    {
        var context = new PipelineContext();
        context.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            new[] { new RepoConnection { Name = repoName } });
        return context;
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
