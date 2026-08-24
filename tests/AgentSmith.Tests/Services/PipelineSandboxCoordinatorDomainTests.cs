using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Tests.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0504: the domain is resolved where the value first exists with no pod created —
/// between applying the scoped contexts and building the specs. An unknown domain
/// refuses there; a known one supplies an image only for the context that declared it.
/// </summary>
public sealed class PipelineSandboxCoordinatorDomainTests
{
    private static readonly DomainProfile Profile = new(
        "sample-domain", "python:3.12-bookworm", [],
        [new DomainProfileCommand("build", "tool build")]);

    private readonly Mock<ISandboxFactory> _factoryMock = new();
    private readonly Mock<ISandboxLanguageResolver> _resolverMock = new();
    private readonly SandboxSpecBuilder _specBuilder =
        new(new StubSandboxResourceResolver(), new StubAgentImageResolver());

    [Fact]
    public async Task Profile_UnknownDomain_RefusesBeforeAnySandbox()
    {
        Discoveries(new RemoteContextDiscovery("warehouse", ".", null, Domain: "not-a-domain"));

        var act = async () => await NewSut().EnsureSandboxesAsync(
            new ResolvedProject(), Context(), CancellationToken.None);

        (await act.Should().ThrowAsync<ConfigurationException>())
            .Which.Message.Should().Contain("not-a-domain");
        _factoryMock.Verify(
            f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MixedProject_DotnetContextAndDataContext_EachSandboxGetsItsOwnImage()
    {
        Discoveries(
            new RemoteContextDiscovery("api", "src/Api", "csharp"),
            new RemoteContextDiscovery("warehouse", "warehouse", null, Domain: "sample-domain"));

        var sandboxes = await NewSut().EnsureSandboxesAsync(
            new ResolvedProject(), Context(), CancellationToken.None);

        sandboxes.Should().HaveCount(2);
        CapturedImages.Should().BeEquivalentTo(
            ["mcr.microsoft.com/dotnet/sdk:9.0", "python:3.12-bookworm"]);
    }

    [Fact]
    public async Task SharedImage_TwoContextsOneSandbox_EachContextKeepsItsOwnDomain()
    {
        Discoveries(
            new RemoteContextDiscovery("warehouse", "warehouse", null, Domain: "sample-domain"),
            new RemoteContextDiscovery("lake", "lake", "python"));

        var context = Context();
        var sandboxes = await NewSut().EnsureSandboxesAsync(
            new ResolvedProject(), context, CancellationToken.None);

        sandboxes.Should().HaveCount(1, "both resolve to the same python image");
        var contexts = context.Get<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.SandboxContexts)[sandboxes.Keys.Single()];
        contexts.Should().HaveCount(2);
        contexts.Single(c => c.ContextName == "warehouse").Domain.Should().Be("sample-domain");
        contexts.Single(c => c.ContextName == "lake").Domain.Should().BeNull();
    }

    [Fact]
    public async Task Profile_NoDomainDeclared_BehavesExactlyAsBefore()
    {
        Discoveries(new RemoteContextDiscovery("api", "src/Api", "csharp"));

        await NewSut().EnsureSandboxesAsync(new ResolvedProject(), Context(), CancellationToken.None);

        CapturedImages.Should().Equal("mcr.microsoft.com/dotnet/sdk:9.0");
    }

    private List<string> CapturedImages { get; } = [];

    private void Discoveries(params RemoteContextDiscovery[] discoveries)
    {
        _resolverMock
            .Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(discoveries);
        _factoryMock
            .Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SandboxSpec spec, CancellationToken _) =>
            {
                CapturedImages.Add(spec.ToolchainImage);
                return new Mock<ISandbox>().Object;
            });
    }

    private static PipelineContext Context()
    {
        var context = new PipelineContext();
        context.Set<IReadOnlyList<RepoConnection>>(
            ContextKeys.Repos, [new RepoConnection { Name = "data-repo" }]);
        return context;
    }

    private PipelineSandboxCoordinator NewSut() => new(
        _factoryMock.Object,
        _specBuilder,
        _resolverMock.Object,
        EventTestStubs.NoOp,
        EventTestStubs.RunContext,
        new NoOpSandboxLivenessSupervisor(),
        TestDomainProfiles.Resolver(Profile),
        NullLogger<PipelineSandboxCoordinator>.Instance);
}
