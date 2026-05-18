using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// p0140c: PollingConfigDeprecationWarner walks the loaded config and emits one warning
/// per project whose deprecated project-level Polling.Enabled is true (post-p0140c those
/// settings are ignored — polling moved to TrackerConnection).
/// </summary>
public sealed class PollingConfigDeprecationWarnerTests
{
    [Fact]
    public void Warn_NoProjectLevelPolling_NoWarning()
    {
        var logger = new Mock<ILogger<PollingConfigDeprecationWarner>>();
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["alpha"] = Project("alpha", "tr-a", pollingEnabled: false),
            }
        };

        new PollingConfigDeprecationWarner(logger.Object).Warn(config);

        VerifyWarningCount(logger, 0);
    }

    [Fact]
    public void Warn_ProjectLevelPollingEnabled_EmitsWarningWithTrackerName()
    {
        var logger = new Mock<ILogger<PollingConfigDeprecationWarner>>();
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["alpha"] = Project("alpha", "tr-alpha", pollingEnabled: true),
            }
        };

        new PollingConfigDeprecationWarner(logger.Object).Warn(config);

        VerifyWarningContaining(logger, "alpha", "tr-alpha");
    }

    [Fact]
    public void Warn_MultipleProjects_EmitsOneWarningEach()
    {
        var logger = new Mock<ILogger<PollingConfigDeprecationWarner>>();
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["alpha"] = Project("alpha", "tr-a", pollingEnabled: true),
                ["beta"]  = Project("beta",  "tr-b", pollingEnabled: true),
                ["gamma"] = Project("gamma", "tr-c", pollingEnabled: true),
            }
        };

        new PollingConfigDeprecationWarner(logger.Object).Warn(config);

        VerifyWarningCount(logger, 3);
    }

    private static ResolvedProject Project(string name, string trackerName, bool pollingEnabled)
        => new()
        {
            Name = name,
            Tracker = new TrackerConnection { Name = trackerName, Type = TrackerType.GitHub },
            Polling = new PollingConfig { Enabled = pollingEnabled, IntervalSeconds = 90 },
        };

    private static void VerifyWarningCount(Mock<ILogger<PollingConfigDeprecationWarner>> logger, int expected)
        => logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(expected));

    private static void VerifyWarningContaining(
        Mock<ILogger<PollingConfigDeprecationWarner>> logger,
        params string[] substrings)
        => logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    substrings.All(s => state.ToString()!.Contains(s))),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
}
