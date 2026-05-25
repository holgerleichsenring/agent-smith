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
/// p0147e: per-service unit tests for IPipelineSandboxCoordinator. Covers
/// lazy boot, idempotency, dispose-exactly-once, and the requirement
/// predicate.
/// </summary>
public sealed class PipelineSandboxCoordinatorTests
{
    private readonly Mock<ISandboxFactory> _factoryMock = new();
    private readonly Mock<ISandboxLanguageResolver> _resolverMock = new();
    private readonly SandboxSpecBuilder _specBuilder
        = new(new StubSandboxResourceResolver(), new StubAgentImageResolver());

    public PipelineSandboxCoordinatorTests()
    {
        _resolverMock.Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
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
        var sandbox = new Mock<ISandbox>().Object;
        _factoryMock.Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sandbox);

        var context = new PipelineContext();
        context.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, new[] { new RepoConnection() });

        var sut = NewSut();
        var result = await sut.EnsureSandboxesAsync(new ResolvedProject(), context, CancellationToken.None);

        result.Values.Should().ContainSingle().Which.Should().BeSameAs(sandbox);
        context.TryGet<ISandbox>(ContextKeys.Sandbox, out var stored).Should().BeTrue();
        stored.Should().BeSameAs(sandbox);
    }

    [Fact]
    public async Task EnsureSandboxesAsync_SecondCall_ReturnsCachedSandbox_FactoryCalledOnce()
    {
        var sandbox = new Mock<ISandbox>().Object;
        _factoryMock.Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sandbox);

        var context = new PipelineContext();
        context.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, new[] { new RepoConnection() });
        var sut = NewSut();

        var first = await sut.EnsureSandboxesAsync(new ResolvedProject(), context, CancellationToken.None);
        var second = await sut.EnsureSandboxesAsync(new ResolvedProject(), context, CancellationToken.None);

        first.Should().BeSameAs(second);
        _factoryMock.Verify(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_DisposesSandboxExactlyOnce_EvenIfCalledTwice()
    {
        var sandboxMock = new Mock<ISandbox>();
        _factoryMock.Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sandboxMock.Object);

        var context = new PipelineContext();
        context.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, new[] { new RepoConnection() });
        var sut = NewSut();
        await sut.EnsureSandboxesAsync(new ResolvedProject(), context, CancellationToken.None);

        await sut.DisposeAsync();
        await sut.DisposeAsync();

        sandboxMock.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_NoSandboxEverBooted_DoesNotThrow()
    {
        var sut = NewSut();
        var act = async () => await sut.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureSandboxesAsync_SourcePathInContext_AttachedToSpecAsInitialSourcePath()
    {
        SandboxSpec? captured = null;
        _factoryMock.Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .Callback<SandboxSpec, CancellationToken>((s, _) => captured = s)
            .ReturnsAsync(new Mock<ISandbox>().Object);

        var context = new PipelineContext();
        context.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, new[] { new RepoConnection() });
        context.Set(ContextKeys.SourcePath, "/tmp/host-clone");
        var sut = NewSut();

        await sut.EnsureSandboxesAsync(new ResolvedProject(), context, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.InitialSourcePath.Should().Be("/tmp/host-clone");
    }

    private PipelineSandboxCoordinator NewSut() => new(
        _factoryMock.Object,
        _specBuilder,
        _resolverMock.Object,
        NullLogger<PipelineSandboxCoordinator>.Instance);
}
