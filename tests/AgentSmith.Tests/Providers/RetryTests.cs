using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Providers;

public class RetryConfigTests
{
    [Fact]
    public void Defaults_AreReasonable()
    {
        var config = new RetryConfig();

        config.MaxRetries.Should().Be(5);
        config.InitialDelayMs.Should().Be(2000);
        config.BackoffMultiplier.Should().Be(2.0);
        config.MaxDelayMs.Should().Be(60000);
    }

    [Fact]
    public void AgentConfig_HasRetryWithDefaults()
    {
        var agentConfig = new AgentConfig();

        agentConfig.Retry.Should().NotBeNull();
        agentConfig.Retry.MaxRetries.Should().Be(5);
    }
}
