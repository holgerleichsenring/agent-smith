using AgentSmith.Contracts.Configuration;
using AgentSmith.Infrastructure.Providers.Agent;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

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

public class ResilientHttpClientFactoryTests
{
    [Fact]
    public void Create_ReturnsNonNullHttpClient()
    {
        var config = new RetryConfig();
        var logger = NullLogger.Instance;
        var factory = new ResilientHttpClientFactory(config, logger);

        var client = factory.Create();

        client.Should().NotBeNull();
    }

    [Fact]
    public void Create_WithCustomConfig_ReturnsClient()
    {
        var config = new RetryConfig
        {
            MaxRetries = 3,
            InitialDelayMs = 500,
            BackoffMultiplier = 1.5,
            MaxDelayMs = 10000
        };
        var logger = NullLogger.Instance;
        var factory = new ResilientHttpClientFactory(config, logger);

        var client = factory.Create();

        client.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_ClientCanSendRequest()
    {
        var config = new RetryConfig { MaxRetries = 1 };
        var logger = NullLogger.Instance;
        var factory = new ResilientHttpClientFactory(config, logger);
        var client = factory.Create();

        // Verify the client is functional by making a request to a known endpoint
        // This will fail with connection refused, but proves the handler chain works
        var act = () => client.GetAsync("http://localhost:1/nonexistent");

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
