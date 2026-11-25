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
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0147e: per-service unit tests for IPipelineSandboxCoordinator. Covers
/// lazy boot, idempotency, dispose-exactly-once, and the requirement
/// predicate.
/// </summary>
public sealed class PipelineSandboxCoordinatorTests
{
    private readonly StubWorkspaceFactory _workspaces = new(new StubSandboxFactory());
    private readonly Mock<ISandboxLanguageResolver> _resolverMock = new();
    private readonly SandboxSpecBuilder _specBuilder
        = new(new StubSandboxResourceResolver(), new StubAgentImageResolver());

    public PipelineSandboxCoordinatorTests()
    {
        _resolverMock.Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<ContextReadBranches>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new RemoteContextDiscovery("default", ".", null) });
    }

    [Fact]
    public void RequiresSandbox_AllCommandsNonRequiring_ReturnsFalse()
    {
        var sut = NewSut();
        var commands = new[] { PipelineCommand.Simple(CommandNames.Triage) };

        sut.RequiresSandbox(commands).Should().BeFalse();
    }

    [Fact]
    public void RequiresSandbox_AnyCommandRequiring_ReturnsTrue()
    {
        var sut = NewSut();
        var commands = new[]
        {
            PipelineCommand.Simple(CommandNames.Triage),
            PipelineCommand.Simple(CommandNames.AgenticExecute)
        };

        sut.RequiresSandbox(commands).Should().BeTrue();
    }

    [Fact]
    public void IsSandboxRequiring_TryCheckoutSourceNotInTheSet()
    {
        // Documented exemption in PipelineSandboxCoordinator: TryCheckoutSource
        // clones host-side via IHostSourceCloner and must NOT trigger an upfront
        // sandbox creation, because the InProcessSandboxFactory needs its
        // resulting SourcePath as the workDir handoff.
        var sut = NewSut();
        sut.IsSandboxRequiring(CommandNames.TryCheckoutSource).Should().BeFalse();
        sut.IsSandboxRequiring(CommandNames.CheckoutSource).Should().BeTrue();
    }

    [Fact]
    public async Task EnsureSandboxesAsync_FirstCall_BootsSandbox_PublishesToContext()
    {
        var context = new PipelineContext();
        context.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, new[] { new RepoConnection() });

        var sut = NewSut();
        var result = await sut.EnsureSandboxesAsync(new ResolvedProject(), context, CancellationToken.None);

        // p0169e: the coordinator wraps each sandbox with SandboxEventProjector
        // so RunStepAsync emits L3 events; identity check is on JobId, not the
        // raw container instance. 2026-11-25-9992b: the key reaches its image's container of the one workspace.
        var container = _workspaces.Created.Should().ContainSingle().Which.Containers.Should().ContainSingle().Subject;
        result.Values.Should().ContainSingle().Which.JobId.Should().Be(container.JobId);
        context.TryGet<ISandbox>(ContextKeys.Sandbox, out var stored).Should().BeTrue();
        stored!.JobId.Should().Be(container.JobId);
    }

    [Fact]
    public async Task EnsureSandboxesAsync_SecondCall_ReturnsCachedSandbox_WorkspaceCreatedOnce()
    {
        var context = new PipelineContext();
        context.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, new[] { new RepoConnection() });
        var sut = NewSut();

        var first = await sut.EnsureSandboxesAsync(new ResolvedProject(), context, CancellationToken.None);
        var firstKeys = first.Keys.ToList();
        var second = await sut.EnsureSandboxesAsync(new ResolvedProject(), context, CancellationToken.None);

        first.Should().BeSameAs(second);
        second.Keys.Should().Equal(firstKeys);
        _workspaces.Created.Should().ContainSingle();
        _workspaces.Sandboxes.Spawned.Should().ContainSingle();
    }

    [Fact]
    public async Task DisposeAsync_DisposesTheWorkspaceExactlyOnce_EvenIfCalledTwice()
    {
        var context = new PipelineContext();
        context.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, new[] { new RepoConnection() });
        var sut = NewSut();
        await sut.EnsureSandboxesAsync(new ResolvedProject(), context, CancellationToken.None);

        await sut.DisposeAsync();
        await sut.DisposeAsync();

        var workspace = _workspaces.Created.Single();
        workspace.DisposeCount.Should().Be(1);
        workspace.Containers.Should().OnlyContain(c => c.Disposed);
    }

    [Fact]
    public async Task DisposeAsync_NoSandboxEverBooted_DoesNotThrow()
    {
        var sut = NewSut();
        var act = async () => await sut.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    // 2026-11-25-9992b: a host source (CLI --source-path) is served at /work/<repo> of the workspace.
    [Fact]
    public async Task EnsureSandboxesAsync_SourcePathInContext_IsTheRepositorysHostSource()
    {
        var context = new PipelineContext();
        context.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, new[] { new RepoConnection { Name = "api" } });
        context.Set(ContextKeys.SourcePath, "/tmp/host-clone");
        var sut = NewSut();

        await sut.EnsureSandboxesAsync(new ResolvedProject(), context, CancellationToken.None);

        _workspaces.Created.Single().Spec.HostSources.Should().BeEquivalentTo(
            new Dictionary<string, string> { ["api"] = "/tmp/host-clone" });
    }

    private PipelineSandboxCoordinator NewSut() =>
        TestCoordinators.Create(_workspaces, _resolverMock.Object, _specBuilder);
}
