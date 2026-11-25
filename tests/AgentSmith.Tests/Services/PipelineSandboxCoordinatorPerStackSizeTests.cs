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
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0336c: same-toolchain-image contexts of a repo run in ONE sandbox even when
/// their context.yaml stack.resources differ — they build sequentially under the
/// single agentic loop, so per-resource pod splitting (the earlier p0268
/// behaviour) bought no parallelism and only inflated the footprint. The one pod
/// is sized to the MAX resource envelope. Uses the REAL SandboxResourceResolver
/// so context.yaml resources actually flow into the resolved SandboxSpec.Resources.
/// </summary>
public sealed class PipelineSandboxCoordinatorPerStackSizeTests
{
    private readonly StubWorkspaceFactory _workspaces = new(new StubSandboxFactory());
    private readonly Mock<ISandboxLanguageResolver> _resolverMock = new();
    private readonly SandboxSpecBuilder _specBuilder = new(
        WiredResourceResolver.Create(),
        new StubAgentImageResolver());

    private static readonly ContextYamlStackResources Heavy = new("500m", "2", "1Gi", "4Gi");
    private static readonly ContextYamlStackResources Light = new("100m", "500m", "256Mi", "512Mi");

    [Fact]
    public async Task SameImageDifferentResources_CollapsesToOnePod_MaxEnvelope()
    {
        _resolverMock.Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<ContextReadBranches>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RemoteContextDiscovery("build", "src/Api", "csharp", Resources: Heavy),
                new RemoteContextDiscovery("scan", "src/Api", "csharp", Resources: Light),
            });

        var context = NewContext("sample-server");
        var sandboxes = await NewSut().EnsureSandboxesAsync(new ResolvedProject(), context, CancellationToken.None);

        // Same image, different size → ONE pod at the max envelope (p0336c).
        _workspaces.Created.Should().ContainSingle("a run has one workspace");
        _workspaces.Sandboxes.Spawned.Should().ContainSingle("one container per distinct image");
        sandboxes.Should().HaveCount(1);
        _workspaces.Sandboxes.Spawned.Single().Spec.Resources.MemoryLimit.Should().Be("4Gi", "the merged pod takes the heavier context's limit");
    }

    [Fact]
    public async Task SameImageSameResources_CollapsesToOneSandbox()
    {
        _resolverMock.Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<ContextReadBranches>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RemoteContextDiscovery("a", "src/A", "csharp", Resources: Heavy),
                new RemoteContextDiscovery("b", "src/B", "csharp", Resources: Heavy),
            });

        var context = NewContext("sample-server");
        var sandboxes = await NewSut().EnsureSandboxesAsync(new ResolvedProject(), context, CancellationToken.None);

        _workspaces.Created.Should().ContainSingle("a run has one workspace");
        _workspaces.Sandboxes.Spawned.Should().ContainSingle("one container per distinct image");
        sandboxes.Should().HaveCount(1);
    }

    private static PipelineContext NewContext(string repoName)
    {
        var context = new PipelineContext();
        context.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            new[] { new RepoConnection { Name = repoName } });
        // p0320a: only code-changing pipelines consume the context.yaml build
        // sizing this suite exercises.
        context.Set(ContextKeys.PipelineName, "code");
        return context;
    }

    private PipelineSandboxCoordinator NewSut() =>
        TestCoordinators.Create(_workspaces, _resolverMock.Object, _specBuilder);
}
