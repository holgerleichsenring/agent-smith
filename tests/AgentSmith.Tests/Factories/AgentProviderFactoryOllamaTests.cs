using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Factories;

public sealed class AgentProviderFactoryOllamaTests
{
    [Fact]
    public void Create_OllamaType_ReturnsOllamaProvider()
    {
        var factory = new AgentProviderFactory(
            new SecretsProvider(),
            NullLoggerFactory.Instance);

        var config = new AgentConfig
        {
            Type = "ollama",
            Model = "qwen2.5-coder:32b",
            Endpoint = "http://localhost:11434"
        };

        var provider = factory.Create(config);

        provider.Should().BeOfType<OllamaAgentProvider>();
        provider.ProviderType.Should().Be("ollama");
    }

    [Fact]
    public void Create_OllamaType_DefaultEndpoint()
    {
        var factory = new AgentProviderFactory(
            new SecretsProvider(),
            NullLoggerFactory.Instance);

        var config = new AgentConfig
        {
            Type = "ollama",
            Model = "mistral-small:3.1"
        };

        var provider = factory.Create(config);

        provider.Should().BeOfType<OllamaAgentProvider>();
    }
}
