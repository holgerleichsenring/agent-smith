using AgentSmith.Application.Services.Preflight.Checks;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Providers;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services.Preflight;

/// <summary>p0324: repo-access probes remote repos and skips local paths.</summary>
public sealed class RepoAccessCheckTests
{
    [Fact]
    public async Task RunAsync_RemoteRepoUnreachable_FailsActionable()
    {
        var provider = new Mock<ISourceProvider>();
        provider.Setup(p => p.ProbeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConnectionProbeResult.Unreachable(120, "authentication failed"));
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);

        var check = new RepoAccessCheck(
            FakePreflightConfigSource.Of(ConfigWithRepo(RepoType.GitHub)), factory.Object);

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Fail);
        result.Message.Should().Contain("authentication failed");
        result.FixHint.Should().Contain("auth");
    }

    [Fact]
    public async Task RunAsync_OnlyLocalRepos_Skips()
    {
        var check = new RepoAccessCheck(
            FakePreflightConfigSource.Of(ConfigWithRepo(RepoType.Local)),
            new StubSourceProviderFactory());

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Skip);
    }

    [Fact]
    public async Task RunAsync_RemoteReachable_Passes()
    {
        var check = new RepoAccessCheck(
            FakePreflightConfigSource.Of(ConfigWithRepo(RepoType.GitHub)),
            new StubSourceProviderFactory());

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Pass);
    }

    private static AgentSmithConfig ConfigWithRepo(RepoType type) => new()
    {
        Repos = new Dictionary<string, RepoConnection>
        {
            ["main"] = new() { Name = "main", Type = type, Url = "https://example.test/r" },
        },
    };
}
