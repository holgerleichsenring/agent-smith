using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Services.Dialogue;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Factories;

public sealed class AgentProviderFactoryOllamaTests
{
    [Fact]
    public void Create_OllamaType_UnreachableEndpoint_ThrowsConfigurationException()
    {
        var factory = new AgentProviderFactory(
            new SecretsProvider(),
            NullLoggerFactory.Instance,
            Mock.Of<IDialogueTransport>(),
            new InMemoryDialogueTrail(),
            new AgentPromptBuilder(new FakePromptCatalog()));

        var config = new AgentConfig
        {
            Type = "ollama",
            Model = "qwen2.5-coder:32b",
            Endpoint = "http://localhost:19999"
        };

        var act = () => factory.Create(config);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*Cannot connect to Ollama*");
    }

    [Fact]
    public void Create_OllamaType_ErrorMessageContainsPullCommand()
    {
        var factory = new AgentProviderFactory(
            new SecretsProvider(),
            NullLoggerFactory.Instance,
            Mock.Of<IDialogueTransport>(),
            new InMemoryDialogueTrail(),
            new AgentPromptBuilder(new FakePromptCatalog()));

        var config = new AgentConfig
        {
            Type = "ollama",
            Model = "mistral-small:3.1",
            Endpoint = "http://localhost:19999"
        };

        var act = () => factory.Create(config);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*ollama pull mistral-small:3.1*");
    }
}
