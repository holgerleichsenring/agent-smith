using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// p0357: CompactionConfigDeprecationWarner emits one warning per agent whose
/// compaction.threshold_iterations deviates from the historical default — the
/// iteration trigger is a no-op now; compaction fires on token pressure only.
/// </summary>
public sealed class CompactionConfigDeprecationWarnerTests
{
    [Fact]
    public void Warn_DefaultThreshold_NoWarning()
    {
        var logger = new Mock<ILogger<CompactionConfigDeprecationWarner>>();
        var config = new AgentSmithConfig
        {
            Agents = new Dictionary<string, AgentConfig>
            {
                ["default"] = new() { Compaction = new CompactionConfig() },
            }
        };

        new CompactionConfigDeprecationWarner(logger.Object).Warn(config);

        VerifyWarningCount(logger, 0);
    }

    [Fact]
    public void Warn_NonDefaultThreshold_EmitsWarningNamingAgent()
    {
        var logger = new Mock<ILogger<CompactionConfigDeprecationWarner>>();
        var config = new AgentSmithConfig
        {
            Agents = new Dictionary<string, AgentConfig>
            {
                ["azure-openai-default"] = new()
                {
                    Compaction = new CompactionConfig { ThresholdIterations = 12 },
                },
            }
        };

        new CompactionConfigDeprecationWarner(logger.Object).Warn(config);

        VerifyWarningContaining(logger, "azure-openai-default", "threshold_iterations");
    }

    private static void VerifyWarningCount(
        Mock<ILogger<CompactionConfigDeprecationWarner>> logger, int expected)
        => logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(expected));

    private static void VerifyWarningContaining(
        Mock<ILogger<CompactionConfigDeprecationWarner>> logger,
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
