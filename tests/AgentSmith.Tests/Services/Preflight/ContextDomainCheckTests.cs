using AgentSmith.Application.Services.Preflight.Checks;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services.Preflight;

/// <summary>
/// p0504: the same question the sandbox coordinator refuses on, answered WITHOUT a run.
/// A stale pin and a typo look identical from a failed run; an operator should not have
/// to burn one to tell them apart.
/// </summary>
public sealed class ContextDomainCheckTests
{
    private static readonly DomainProfile Profile = new(
        "sample-domain", "python:3.12-bookworm", [],
        [new DomainProfileCommand("build", "tool build")]);

    [Fact]
    public async Task RunAsync_UnknownDomainDeclared_FailsNamingTheCatalog()
    {
        var check = Check(new RemoteContextDiscovery("warehouse", ".", null, Domain: "not-a-domain"));

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Fail);
        result.Message.Should().Contain("not-a-domain").And.Contain("warehouse");
        result.FixHint.Should().Contain("(test catalog)").And.Contain("sample-domain");
    }

    [Fact]
    public async Task RunAsync_KnownDomainDeclared_PassesNamingTheImage()
    {
        var check = Check(new RemoteContextDiscovery("warehouse", ".", null, Domain: "sample-domain"));

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Pass);
        result.Message.Should().Contain("python:3.12-bookworm");
    }

    [Fact]
    public async Task RunAsync_NoContextDeclaresADomain_Skips()
    {
        var check = Check(new RemoteContextDiscovery("default", ".", "csharp"));

        (await check.RunAsync(CancellationToken.None)).Status.Should().Be(PreflightStatus.Skip);
    }

    private static ContextDomainCheck Check(params RemoteContextDiscovery[] discoveries)
    {
        var resolver = new Mock<ISandboxLanguageResolver>();
        resolver
            .Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(discoveries);
        var config = new AgentSmithConfig
        {
            Repos = new Dictionary<string, RepoConnection>
            {
                ["data-repo"] = new() { Name = "data-repo", Url = "https://example.test/r" },
            },
        };
        return new ContextDomainCheck(
            FakePreflightConfigSource.Of(config), resolver.Object, new TestDomainProfiles(Profile));
    }
}
