using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Providers;

public class CacheConfigTests
{
    [Fact]
    public void Defaults_CacheEnabled()
    {
        var config = new CacheConfig();

        config.IsEnabled.Should().BeTrue();
        config.Strategy.Should().Be("automatic");
    }

    [Fact]
    public void AgentConfig_HasCacheWithDefaults()
    {
        var agentConfig = new AgentConfig();

        agentConfig.Cache.Should().NotBeNull();
        agentConfig.Cache.IsEnabled.Should().BeTrue();
    }
}
