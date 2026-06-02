using System.Text.RegularExpressions;
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
/// p0194: end-to-end through the Coordinator. A resolver returning a
/// non-csharp language (typescript here — the one that bit the operator)
/// must produce a SandboxSpec whose ToolchainImage bundles git.
/// Companion to the static allowlist test
/// <see cref="AgentSmith.Tests.Sandbox.SandboxSpecBuilderImageBundlesGitTests"/>;
/// catches regressions even if a future refactor splits the language→image
/// resolution into a separate code path that bypasses the static dict.
/// </summary>
public sealed class PipelineSandboxCoordinatorImageBundlesGitTests
{
    private static readonly Regex GitBearing = new(
        @"^mcr\.microsoft\.com/dotnet/sdk:|:[^-]*-bookworm$|:[^-]*-bullseye$|^buildpack-deps:[^-]+-scm$",
        RegexOptions.Compiled);

    [Theory]
    [InlineData("csharp")]
    [InlineData("typescript")]
    [InlineData("python")]
    [InlineData("go")]
    [InlineData("rust")]
    public async Task EveryKnownLanguage_ProducesGitBearingToolchainImage(string language)
    {
        var factoryMock = new Mock<ISandboxFactory>();
        SandboxSpec? captured = null;
        factoryMock.Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .Callback<SandboxSpec, CancellationToken>((spec, _) => captured = spec)
            .ReturnsAsync(new Mock<ISandbox>().Object);

        var resolverMock = new Mock<ISandboxLanguageResolver>();
        resolverMock.Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new RemoteContextDiscovery("default", ".", language) });

        var sut = new PipelineSandboxCoordinator(
            factoryMock.Object,
            new SandboxSpecBuilder(new StubSandboxResourceResolver(), new StubAgentImageResolver()),
            resolverMock.Object,
            AgentSmith.Tests.TestHelpers.EventTestStubs.NoOp,
            AgentSmith.Tests.TestHelpers.EventTestStubs.RunContext,
            new NoOpSandboxLivenessSupervisor(),
            NullLogger<PipelineSandboxCoordinator>.Instance);

        var context = new PipelineContext();
        context.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            new[] { new RepoConnection { Name = "any-repo" } });

        await sut.EnsureSandboxesAsync(new ResolvedProject(), context, CancellationToken.None);

        captured.Should().NotBeNull($"coordinator must call factory.CreateAsync for language '{language}'");
        GitBearing.IsMatch(captured!.ToolchainImage).Should().BeTrue(
            $"language '{language}' resolved to '{captured.ToolchainImage}' which does not match a " +
            $"git-bearing base. CheckoutSourceHandler runs `git clone` INSIDE the sandbox; " +
            $"a slim/alpine/bare-tag image will break checkout. See SandboxSpecBuilderImageBundlesGitTests " +
            $"for the canonical allowlist.");
    }
}
