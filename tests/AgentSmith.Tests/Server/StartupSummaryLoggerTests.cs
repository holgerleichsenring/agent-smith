using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0140c: StartupSummaryLogger emits per-tracker polling lines instead of per-project ones,
/// and now flags any project that still carries the deprecated project-level Polling block.
/// </summary>
public sealed class StartupSummaryLoggerTests
{
    [Fact]
    public void Log_PerTrackerPolling_ListsServingProjects()
    {
        var tracker = new TrackerConnection
        {
            Name = "shared-jira",
            Type = TrackerType.Jira,
            Polling = new PollingConfig { Enabled = true, IntervalSeconds = 60 },
        };
        var config = new AgentSmithConfig
        {
            Trackers = new Dictionary<string, TrackerConnection> { ["shared-jira"] = tracker },
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["alpha"] = MakeProject("alpha", tracker),
                ["beta"]  = MakeProject("beta",  tracker),
            }
        };
        var captured = CaptureLogs();

        StartupSummaryLogger.Log(config, captured.Logger);

        captured.Lines.Should().Contain(line =>
            line.Contains("polling tracker 'shared-jira'")
            && line.Contains("alpha")
            && line.Contains("beta"));
    }

    [Fact]
    public void Log_DeprecatedProjectLevelPolling_FlagsInline()
    {
        var tracker = new TrackerConnection { Name = "tr", Type = TrackerType.GitHub };
        var config = new AgentSmithConfig
        {
            Trackers = new Dictionary<string, TrackerConnection> { ["tr"] = tracker },
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["alpha"] = MakeProject("alpha", tracker,
                    polling: new PollingConfig { Enabled = true, IntervalSeconds = 60 }),
            }
        };
        var captured = CaptureLogs();

        StartupSummaryLogger.Log(config, captured.Logger);

        captured.Lines.Should().Contain(line =>
            line.Contains("alpha") && line.Contains("DEPRECATED project-level polling"));
    }

    private static ResolvedProject MakeProject(string name, TrackerConnection tracker, PollingConfig? polling = null)
        => new()
        {
            Name = name,
            Tracker = tracker,
            Agent = new AgentConfig { Type = "Claude", Model = "sonnet" },
            Repo = new RepoConnection { Name = name + "-repo", Url = $"https://example/{name}", Type = RepoType.GitHub },
            Pipeline = "fix-bug",
            Polling = polling ?? new PollingConfig(),
        };

    private static (ILogger Logger, List<string> Lines) CaptureLogs()
    {
        var lines = new List<string>();
        var logger = new Mock<ILogger>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        logger.Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(inv =>
            {
                var state = inv.Arguments[2];
                var exception = (Exception?)inv.Arguments[3];
                var formatter = inv.Arguments[4];
                var invokeMethod = formatter.GetType().GetMethod("Invoke")!;
                var line = (string)invokeMethod.Invoke(formatter, new object?[] { state, exception })!;
                lines.Add(line);
            }));
        return (logger.Object, lines);
    }
}
