using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Services.Configuration;
using AgentSmith.Infrastructure.Services.Factories;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Factories;

public class FactoryTests
{
    [Fact]
    public void TicketProviderFactory_UnknownType_ThrowsConfigurationException()
    {
        var secrets = new SecretsProvider();
        var factory = new TicketProviderFactory(secrets, new Mock<IHttpClientFactory>().Object, NullLoggerFactory.Instance);
        var config = new TicketConfig { Type = "unknown" };

        var act = () => factory.Create(config);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*Unknown ticket provider type*");
    }

    [Fact]
    public void SourceProviderFactory_UnknownType_ThrowsConfigurationException()
    {
        var secrets = new SecretsProvider();
        var factory = new SourceProviderFactory(secrets, new Mock<IHttpClientFactory>().Object, NullLoggerFactory.Instance);
        var config = new SourceConfig { Type = "unknown" };

        var act = () => factory.Create(config);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*Unknown source provider type*");
    }

    [Fact]
    public void SourceProviderFactory_LocalType_ReturnsLocalProvider()
    {
        var secrets = new SecretsProvider();
        var factory = new SourceProviderFactory(secrets, new Mock<IHttpClientFactory>().Object, NullLoggerFactory.Instance);
        var config = new SourceConfig { Type = "local", Path = "/tmp" };

        var provider = factory.Create(config);

        provider.ProviderType.Should().Be("Local");
    }

    [Theory]
    [InlineData("jira")]
    [InlineData("gitlab")]
    public void TicketProviderFactory_NewTypes_RecognizedButNeedSecrets(string type)
    {
        var secrets = new SecretsProvider();
        var factory = new TicketProviderFactory(secrets, new Mock<IHttpClientFactory>().Object, NullLoggerFactory.Instance);
        var config = new TicketConfig { Type = type, Url = "https://example.com", Project = "test" };

        var act = () => factory.Create(config);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*environment variable*");
    }

    [Theory]
    [InlineData("gitlab")]
    [InlineData("azurerepos")]
    public void SourceProviderFactory_NewTypes_RecognizedButNeedSecrets(string type)
    {
        var secrets = new SecretsProvider();
        var factory = new SourceProviderFactory(secrets, new Mock<IHttpClientFactory>().Object, NullLoggerFactory.Instance);
        var config = new SourceConfig { Type = type, Url = "https://dev.azure.com/org/proj/_git/repo" };

        var act = () => factory.Create(config);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*environment variable*");
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
