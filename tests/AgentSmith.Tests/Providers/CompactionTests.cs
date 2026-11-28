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
        config.MaxContextTokens.Should().Be(200000);
        config.MaxContextTokensTriggerRatio.Should().Be(0.7);
        config.KeepRecentIterations.Should().Be(3);
    }

    [Fact]
    public void Properties_AreSettable()
    {
        var config = new CompactionConfig
        {
            IsEnabled = false,
            MaxContextTokens = 100000,
            KeepRecentIterations = 5,
        };

        config.IsEnabled.Should().BeFalse();
        config.MaxContextTokens.Should().Be(100000);
        config.KeepRecentIterations.Should().Be(5);
    }
}
