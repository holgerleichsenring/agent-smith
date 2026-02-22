using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Providers;

public class CompactionConfigTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var config = new CompactionConfig();

        config.IsEnabled.Should().BeTrue();
        config.ThresholdIterations.Should().Be(8);
        config.MaxContextTokens.Should().Be(80000);
        config.KeepRecentIterations.Should().Be(3);
        config.SummaryModel.Should().Be("claude-haiku-4-5-20251001");
    }

    [Fact]
    public void Properties_AreSettable()
    {
        var config = new CompactionConfig
        {
            IsEnabled = false,
            ThresholdIterations = 12,
            MaxContextTokens = 100000,
            KeepRecentIterations = 5,
            SummaryModel = "custom-model"
        };

        config.IsEnabled.Should().BeFalse();
        config.ThresholdIterations.Should().Be(12);
        config.MaxContextTokens.Should().Be(100000);
        config.KeepRecentIterations.Should().Be(5);
        config.SummaryModel.Should().Be("custom-model");
    }
}
