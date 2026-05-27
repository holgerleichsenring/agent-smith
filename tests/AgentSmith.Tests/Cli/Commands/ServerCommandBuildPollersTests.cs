using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Server.Services;
using EnvelopeProjectResolverImpl = AgentSmith.Application.Services.Triggers.ProjectResolver;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Cli.Commands;

/// <summary>
/// p0140c: PollerFactory.Build is now per-tracker (one TrackerPoller per polling-enabled
/// TrackerConnection). Verifies (a) tracker.Polling.Enabled gates registration, (b) one
/// poller is produced per enabled tracker regardless of how many projects share it, and
/// (c) the same flow works end-to-end from a real YAML config.
/// </summary>
public sealed class ServerCommandBuildPollersTests
{
    [Fact]
    public void BuildPollers_GitHubTrackerWithPolling_RegistersOneTrackerPoller()
    {
        var pollers = Build(TrackerType.GitHub).ToList();
        pollers.Should().HaveCount(1);
        pollers[0].Should().BeOfType<TrackerPoller>();
        pollers[0].PlatformName.Should().Be("GitHub");
    }

    [Fact]
    public void BuildPollers_AzureDevOpsTrackerWithPolling_RegistersOneTrackerPoller()
    {
        var pollers = Build(TrackerType.AzureDevOps).ToList();
        pollers.Should().HaveCount(1);
        pollers[0].Should().BeOfType<TrackerPoller>();
        pollers[0].PlatformName.Should().Be("AzureDevOps");
    }

    [Fact]
    public void BuildPollers_GitLabTrackerWithPolling_RegistersOneTrackerPoller()
    {
        var pollers = Build(TrackerType.GitLab).ToList();
        pollers.Should().HaveCount(1);
        pollers[0].Should().BeOfType<TrackerPoller>();
        pollers[0].PlatformName.Should().Be("GitLab");
    }

    [Fact]
    public void BuildPollers_JiraTrackerWithPolling_RegistersOneTrackerPoller()
    {
        var pollers = Build(TrackerType.Jira).ToList();
        pollers.Should().HaveCount(1);
        pollers[0].Should().BeOfType<TrackerPoller>();
        pollers[0].PlatformName.Should().Be("Jira");
    }

    [Fact]
    public void BuildPollers_PollingDisabled_RegistersNothing()
    {
        var pollers = Build(TrackerType.GitHub, pollingEnabled: false).ToList();
        pollers.Should().BeEmpty();
    }

    /// <summary>
    /// End-to-end smoke: real YAML on disk -> real YamlConfigurationLoader -> PollerFactory.Build
    /// -> one TrackerPoller per polling-enabled tracker (4 trackers here), no Redis/network.
    /// </summary>
    [Fact]
    public void BuildPollers_LoadsYamlConfig_RegistersOnePerTracker()
    {
        var yaml = """
            agents:
              a: { type: Claude }
            repos:
              gh-repo: { type: GitHub, url: https://github.com/o/r, auth: token }
              azdo-repo: { type: AzureDevOps, url: https://dev.azure.com/o/p/_git/r, auth: pat }
              gl-repo: { type: GitLab, url: https://gitlab.com/g/r, auth: token }
            trackers:
              gh-tr: { type: GitHub, url: https://github.com/o/r, auth: token, polling: { enabled: true } }
              azdo-tr: { type: AzureDevOps, organization: https://dev.azure.com/o, project: p, auth: pat, polling: { enabled: true } }
              gl-tr: { type: GitLab, project: g/r, auth: token, polling: { enabled: true } }
              jr-tr: { type: Jira, url: https://jira.example, project: PROJ, auth: token, polling: { enabled: true } }
            projects:
              gh:
                agent: a
                tracker: gh-tr
                repos: [gh-repo]
                pipeline: fix-bug
              azdo:
                agent: a
                tracker: azdo-tr
                repos: [azdo-repo]
                pipeline: fix-bug
              gl:
                agent: a
                tracker: gl-tr
                repos: [gl-repo]
                pipeline: fix-bug
              jr:
                agent: a
                tracker: jr-tr
                repos: [gh-repo]
                pipeline: fix-bug
            """;

        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-dry-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, yaml);

        try
        {
            var config = new YamlConfigurationLoader(new ProjectConfigNormalizer(), new ConfigCatalogResolver(), new AgentSmithPaths())
                .LoadConfig(path);

            var provider = BuildProvider();
            var pollers = PollerFactory.Build(provider, config).ToList();

            pollers.Should().HaveCount(4);
            pollers.Should().AllBeOfType<TrackerPoller>();
            pollers.Select(p => p.PlatformName).Should().BeEquivalentTo(
                new[] { "GitHub", "AzureDevOps", "GitLab", "Jira" });
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Two projects sharing one polling-enabled tracker => exactly one TrackerPoller
    /// (vs. pre-p0140c per-project enumeration that produced N).
    /// </summary>
    [Fact]
    public void BuildPollers_TwoProjectsSharingTracker_RegistersOnePoller()
    {
        var tracker = new TrackerConnection
        {
            Name = "shared",
            Type = TrackerType.GitHub,
            Polling = new PollingConfig { Enabled = true }
        };
        var config = new AgentSmithConfig
        {
            Trackers = new Dictionary<string, TrackerConnection> { ["shared"] = tracker },
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["alpha"] = new ResolvedProject { Name = "alpha", Tracker = tracker },
                ["beta"]  = new ResolvedProject { Name = "beta",  Tracker = tracker },
            }
        };
        var pollers = PollerFactory.Build(BuildProvider(), config).ToList();
        pollers.Should().HaveCount(1);
        pollers[0].TrackerName.Should().Be("shared");
    }

    private static IEnumerable<IEventPoller> Build(TrackerType trackerType, bool pollingEnabled = true)
    {
        var tracker = new TrackerConnection
        {
            Name = trackerType.ToString().ToLowerInvariant() + "-tr",
            Type = trackerType,
            Polling = new PollingConfig { Enabled = pollingEnabled }
        };
        var config = new AgentSmithConfig
        {
            Trackers = new Dictionary<string, TrackerConnection> { [tracker.Name] = tracker }
        };
        return PollerFactory.Build(BuildProvider(), config);
    }

    private static IServiceProvider BuildProvider()
    {
        var ticketFactory = new Mock<ITicketProviderFactory>();
        var envelopeResolver = new EnvelopeProjectResolverImpl();
        var spawnUseCase = new Mock<ISpawnPipelineRunsUseCase>();

        var services = new ServiceCollection();
        services.AddSingleton(ticketFactory.Object);
        services.AddSingleton<IEnvelopeProjectResolver>(envelopeResolver);
        services.AddSingleton(spawnUseCase.Object);
        services.AddSingleton<ISystemEventPublisher, NoOpSystemEventPublisher>();
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);
        return services.BuildServiceProvider();
    }
}
