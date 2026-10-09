using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Webhooks;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework.PrSweep;

/// <summary>2026-10-08-10b0: the deadline's cut moves on to the next repository; a command names only allowed projects.</summary>
public sealed class PrSweepRotationTests : IDisposable
{
    private readonly SettableClock _clock = new() { Now = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero) };
    private readonly ServerStateStore _store;

    public PrSweepRotationTests() => _store = new ServerStateStore(_clock);

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task PrSweep_BudgetCut_ResumesNextRepository()
    {
        var read = new List<string>();
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(s => s.Create(It.IsAny<RepoConnection>())).Returns((RepoConnection repo) =>
        {
            var lister = new Mock<IOpenPullRequestLister>();
            lister.Setup(l => l.ListOpenAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => { read.Add(repo.Name); _clock.Now += TimeSpan.FromMinutes(5); return new OpenPullRequestsPage([]); });
            var provider = new Mock<ISourceProvider>();
            provider.As<IOpenPullRequestSource>().Setup(p => p.OpenPullRequests()).Returns(lister.Object);
            return provider.Object;
        });
        var tracker = new TrackerConnection { Name = "t", Polling = new PollingConfig { Enabled = true } };
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["p"] = new() { Name = "p", Tracker = tracker, Repos = [
                    new RepoConnection { Name = "a", Url = "https://github.com/o/a", Type = RepoType.GitHub },
                    new RepoConnection { Name = "b", Url = "https://github.com/o/b", Type = RepoType.GitHub }] },
            },
        };
        var admission = new PrCommentCommandAdmission(new AgentSmith.Application.Webhooks.CommentIntentParser(Mock.Of<IIntentParser>()),
            new ServerContext("c.yml"), Mock.Of<IPrCommandLaunch>(), NullLogger<PrCommentCommandAdmission>.Instance);
        var actions = new AgentSmith.Server.Services.Sweep.PrSweepActions(new PrReviewRouteResolver(new ConfiguredRepoFinder()),
            new PrTriggerLabelResolver(), new PrRunContextFactory(), Mock.Of<IDetachedPipelineLauncher>(), admission,
            new AgentSmith.Server.Services.Sweep.PrSweepBreaker(Mock.Of<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(), Mock.Of<ISourceProviderFactory>(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentSmith.Server.Services.Sweep.PrSweepBreaker>.Instance), Mock.Of<IServiceProvider>());
        var sweep = new AgentSmith.Server.Services.Sweep.PrSweep(_store.ScopeFactory, sources.Object, actions, new PrTriggerLabelResolver(), _clock,
            NullLogger<AgentSmith.Server.Services.Sweep.PrSweep>.Instance);

        await sweep.RunAsync(config, [tracker], TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), CancellationToken.None);
        await sweep.RunAsync(config, [tracker], TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), CancellationToken.None);

        read.Should().Equal("a", "b");
    }

    [Fact]
    public async Task PrCommand_ModelNamedForeignProject_NotHandled()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["p"] = new() { Name = "p", Repos = [new RepoConnection { Name = "r", Url = "https://github.com/o/r", Type = RepoType.GitHub }] },
                ["q"] = new() { Name = "q", Repos = [new RepoConnection { Name = "r", Url = "https://github.com/o/r", Type = RepoType.GitHub }] },
            },
        });
        var launch = new PrCommandLaunch(loader.Object, new ServerContext("c.yml"), new ConfiguredRepoFinder(), Mock.Of<IActiveRunLease>(),
            Mock.Of<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(), Mock.Of<ISourceProviderFactory>(), NullLogger<PrCommandLaunch>.Instance);
        var command = new PrCommentCommand("/as fix", new PrCommentAuthor("https://github.com/o/r", "o/r", "alice", "alice"), "r#4", "pr:r#4", "4");

        var result = await launch.LaunchAsync(command, new PipelineRequest("q", "fix-bug"), CancellationToken.None, new HashSet<string> { "p" });

        result.Handled.Should().BeFalse();
    }
}
