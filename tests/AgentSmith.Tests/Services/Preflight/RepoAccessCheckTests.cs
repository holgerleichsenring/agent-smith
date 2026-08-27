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

    [Fact]
    public async Task Preflight_AConnectionThatCannotBeReached_IsReported()
    {
        // 2026-08-27-7098: an installation that discovers its repositories through a
        // connection declares none individually, so this check used to skip itself on
        // exactly the installations whose start later died on an unreachable remote.
        var probed = new List<RepoConnection>();
        var check = new RepoAccessCheck(
            FakePreflightConfigSource.Of(ConfigWithConnectionDiscoveredRepo()),
            UnreachableFactory("host is unreachable", probed));

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Fail);
        result.Message.Should().Contain("host is unreachable");
        probed.Should().ContainSingle().Which.Name.Should().Be("discovered-service");
    }

    [Fact]
    public async Task Preflight_AConnectionThatAnswers_IsSilent()
    {
        var check = new RepoAccessCheck(
            FakePreflightConfigSource.Of(ConfigWithConnectionDiscoveredRepo()),
            new StubSourceProviderFactory());

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Pass);
        result.Message.Should().Contain("discovered-service");
    }

    [Fact]
    public async Task Preflight_NoConnectionConfigured_SkipsWithItsReason()
    {
        var check = new RepoAccessCheck(
            FakePreflightConfigSource.Of(new AgentSmithConfig()), new StubSourceProviderFactory());

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Skip);
        result.Message.Should().Contain("no remote repo or connection configured");
    }

    [Fact]
    public async Task RunAsync_OneRemoteReachedByTwoProjects_IsProbedOnce()
    {
        var probed = new List<RepoConnection>();
        var config = ConfigWithConnectionDiscoveredRepo();
        config.Projects["two"] = new ResolvedProject
        {
            Name = "two",
            Repos = config.Projects["one"].Repos,
        };

        await new RepoAccessCheck(
            FakePreflightConfigSource.Of(config),
            UnreachableFactory("host is unreachable", probed)).RunAsync(CancellationToken.None);

        probed.Should().ContainSingle("probing one remote twice proves nothing new");
    }

    private static ISourceProviderFactory UnreachableFactory(string error, List<RepoConnection> probed)
    {
        var provider = new Mock<ISourceProvider>();
        provider.Setup(p => p.ProbeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConnectionProbeResult.Unreachable(120, error));
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>()))
            .Callback<RepoConnection>(probed.Add)
            .Returns(provider.Object);
        return factory.Object;
    }

    /// <summary>What RepoGlobExpander leaves behind: the project carries the discovered
    /// repository as a full connection, and config.Repos names nothing at all.</summary>
    private static AgentSmithConfig ConfigWithConnectionDiscoveredRepo() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["one"] = new()
            {
                Name = "one",
                Repos =
                [
                    new RepoConnection
                    {
                        Name = "discovered-service",
                        Type = RepoType.AzureDevOps,
                        Url = "https://example.test/discovered-service",
                        Auth = "connection-token",
                    },
                ],
            },
        },
    };

    private static AgentSmithConfig ConfigWithRepo(RepoType type) => new()
    {
        Repos = new Dictionary<string, RepoConnection>
        {
            ["main"] = new() { Name = "main", Type = type, Url = "https://example.test/r" },
        },
    };
}
