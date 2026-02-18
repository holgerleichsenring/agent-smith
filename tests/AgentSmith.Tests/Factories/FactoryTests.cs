using AgentSmith.Contracts.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Configuration;
using AgentSmith.Infrastructure.Factories;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Factories;

public class FactoryTests
{
    [Fact]
    public void TicketProviderFactory_UnknownType_ThrowsConfigurationException()
    {
        var secrets = new SecretsProvider();
        var factory = new TicketProviderFactory(secrets, NullLoggerFactory.Instance);
        var config = new TicketConfig { Type = "unknown" };

        var act = () => factory.Create(config);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*Unknown ticket provider type*");
    }

    [Fact]
    public void SourceProviderFactory_UnknownType_ThrowsConfigurationException()
    {
        var secrets = new SecretsProvider();
        var factory = new SourceProviderFactory(secrets, NullLoggerFactory.Instance);
        var config = new SourceConfig { Type = "unknown" };

        var act = () => factory.Create(config);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*Unknown source provider type*");
    }

    [Fact]
    public void SourceProviderFactory_LocalType_ReturnsLocalProvider()
    {
        var secrets = new SecretsProvider();
        var factory = new SourceProviderFactory(secrets, NullLoggerFactory.Instance);
        var config = new SourceConfig { Type = "local", Path = "/tmp" };

        var provider = factory.Create(config);

        provider.ProviderType.Should().Be("Local");
    }

    [Fact]
    public void AgentProviderFactory_UnknownType_ThrowsConfigurationException()
    {
        var secrets = new SecretsProvider();
        var factory = new AgentProviderFactory(secrets, NullLoggerFactory.Instance);
        var config = new AgentConfig { Type = "unknown" };

        var act = () => factory.Create(config);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*Unknown agent provider type*");
    }
}
