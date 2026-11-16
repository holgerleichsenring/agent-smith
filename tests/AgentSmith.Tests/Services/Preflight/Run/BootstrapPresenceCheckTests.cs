using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Preflight.Run;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Preflight.Run;

/// <summary>
/// 2026-11-16-f3fba: the bootstrap is read on the host before any sandbox; an uninitialised repository is
/// bootstrap-missing with the project to initialise, an unanswered one readiness-unknown.
/// </summary>
public sealed class BootstrapPresenceCheckTests
{
    private readonly Mock<ISourceProvider> _host = new();
    private readonly Mock<ISandboxLanguageResolver> _contexts = new();

    public BootstrapPresenceCheckTests()
    {
        _contexts.Setup(c => c.ListContextsAsync(It.IsAny<RepoConnection>(), It.IsAny<ContextReadBranches>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RemoteContextListing([new RemoteContextDiscovery("api", ".", "csharp")]));
    }

    private void Serves(bool principles) =>
        _host.Setup(p => p.TryReadFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, CancellationToken _) => path.EndsWith("/principles.md") && !principles ? null : "content");

    private Task<RunPreflightFinding> RunAsync()
    {
        var hosts = new Mock<ISourceProviderFactory>();
        hosts.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(_host.Object);
        var probe = new RemoteBootstrapProbe(hosts.Object, _contexts.Object, NullLogger<RemoteBootstrapProbe>.Instance);
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            [new RepoConnection { Name = "web-api", Url = "https://github.com/o/web-api", Type = RepoType.GitHub }]);
        pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject { Name = "demo" });
        return new BootstrapPresenceCheck(new RepoReadiness(probe, hosts.Object, NullLogger<RepoReadiness>.Instance))
            .RunAsync(pipeline, CancellationToken.None);
    }

    [Fact]
    public async Task RunAsync_Bootstrapped_Passes()
    {
        Serves(principles: true);

        (await RunAsync()).Verdict.Should().Be(RunPreflightVerdict.Pass);
    }

    [Fact]
    public async Task RunAsync_Uninitialised_FailsBootstrapMissingNamingTheRepoAndProject()
    {
        Serves(principles: false);

        var finding = await RunAsync();

        finding.Verdict.Should().Be(RunPreflightVerdict.Fail);
        finding.Kind.Should().Be(RunEndKinds.BootstrapMissing);
        finding.Message.Should().Contain("web-api");
        finding.Detail.Should().Be(new EndDetail("demo", finding.Detail!.Repos, "Initialise", "demo", "/config/projects"));
        finding.Detail.Repos.Should().Equal("web-api");
    }

    [Fact]
    public async Task RunAsync_HostUnreadable_FailsReadinessUnknown()
    {
        _contexts.Setup(c => c.ListContextsAsync(It.IsAny<RepoConnection>(), It.IsAny<ContextReadBranches>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("host down"));

        var finding = await RunAsync();

        finding.Verdict.Should().Be(RunPreflightVerdict.Fail);
        finding.Kind.Should().Be(RunEndKinds.ReadinessUnknown);
        finding.Detail!.Subject.Should().Contain("host down");
        finding.Detail.Control.Should().BeNull("no setting lifts an unreachable host");
    }

    [Fact]
    public void NeedsSandbox_IsFalse() =>
        new BootstrapPresenceCheck(null!).NeedsSandbox.Should().BeFalse();
}
