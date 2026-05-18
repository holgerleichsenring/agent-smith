using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0140d: StartupSummaryLogger.LogProject emits one summary line per project
/// ("• {Project}: {RepoCount} repo(s), tickets=...") followed by one indented
/// line per repo ("- {Repo}: {Source}"). These tests pin that contract so
/// regressions to the pre-p0140d single-line shape (or to skipping repos in
/// multi-repo projects) trip immediately.
/// </summary>
public sealed class StartupSummaryLoggerMultiRepoTests
{
    [Fact]
    public void Log_MultiRepoProject_EmitsOneLinePerRepo()
    {
        var tracker = new TrackerConnection { Name = "tr", Type = TrackerType.GitHub };
        var project = new ResolvedProject
        {
            Name = "multi",
            Tracker = tracker,
            Agent = new AgentConfig { Type = "Claude", Model = "sonnet" },
            Repos = new[]
            {
                new RepoConnection { Name = "repo-a", Url = "https://example/a", Type = RepoType.GitHub },
                new RepoConnection { Name = "repo-b", Url = "https://example/b", Type = RepoType.GitHub },
                new RepoConnection { Name = "repo-c", Url = "https://example/c", Type = RepoType.GitHub },
            },
            Pipeline = "fix-bug",
        };
        var config = new AgentSmithConfig
        {
            Trackers = new Dictionary<string, TrackerConnection> { ["tr"] = tracker },
            Projects = new Dictionary<string, ResolvedProject> { ["multi"] = project }
        };
        var captured = CaptureLogs();

        StartupSummaryLogger.Log(config, captured.Logger);

        captured.Lines.Should().Contain(line =>
            line.Contains("multi") && line.Contains("3 repo(s)"));
        captured.Lines.Should().Contain(l => l.Contains("repo-a") && l.Contains("https://example/a"));
        captured.Lines.Should().Contain(l => l.Contains("repo-b") && l.Contains("https://example/b"));
        captured.Lines.Should().Contain(l => l.Contains("repo-c") && l.Contains("https://example/c"));
    }

    [Fact]
    public void Log_SingleRepoProject_EmitsOneRepoLine()
    {
        var tracker = new TrackerConnection { Name = "tr", Type = TrackerType.GitHub };
        var project = new ResolvedProject
        {
            Name = "solo",
            Tracker = tracker,
            Agent = new AgentConfig { Type = "Claude", Model = "sonnet" },
            Repos = new[]
            {
                new RepoConnection { Name = "only-repo", Url = "https://example/only", Type = RepoType.GitHub },
            },
            Pipeline = "fix-bug",
        };
        var config = new AgentSmithConfig
        {
            Trackers = new Dictionary<string, TrackerConnection> { ["tr"] = tracker },
            Projects = new Dictionary<string, ResolvedProject> { ["solo"] = project }
        };
        var captured = CaptureLogs();

        StartupSummaryLogger.Log(config, captured.Logger);

        captured.Lines.Should().Contain(line =>
            line.Contains("solo") && line.Contains("1 repo(s)"));
        captured.Lines.Count(l => l.Contains("only-repo") && l.Contains("https://example/only"))
            .Should().Be(1);
    }

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
