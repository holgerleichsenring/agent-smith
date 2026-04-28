using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Dialogue;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Tests.TestSupport;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Factories;

public class AgentProviderFactoryTests : IDisposable
{
    private readonly SecretsProvider _secrets = new();
    private readonly AgentProviderFactory _sut;

    public AgentProviderFactoryTests()
    {
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "test-key");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", "test-key");
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", "test-key");
        _sut = new AgentProviderFactory(_secrets, NullLoggerFactory.Instance,
            Mock.Of<IDialogueTransport>(), new InMemoryDialogueTrail(),
            new AgentPromptBuilder(new FakePromptCatalog()));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", null);
    }

    [Theory]
    [InlineData("claude", "Claude")]
    [InlineData("anthropic", "Claude")]
    [InlineData("openai", "OpenAI")]
    [InlineData("gemini", "Gemini")]
    [InlineData("google", "Gemini")]
    public void Create_ValidType_ReturnsCorrectProvider(string type, string expectedProviderType)
    {
        var config = new AgentConfig { Type = type, Model = "test-model" };
        var provider = _sut.Create(config);
        provider.ProviderType.Should().Be(expectedProviderType);
    }

    [Theory]
    [InlineData("azure-openai")]
    [InlineData("azure")]
    public void Create_AzureOpenAi_ReturnsAzureProvider(string type)
    {
        var config = new AgentConfig
        {
            Type = type, Model = "gpt-4.1",
            Deployment = "gpt4-1-deployment",
            Endpoint = "https://my-instance.openai.azure.com/"
        };
        var provider = _sut.Create(config);
        provider.ProviderType.Should().Be("AzureOpenAI");
    }

    [Fact]
    public void Create_AzureOpenAi_MissingEndpoint_Throws()
    {
        var config = new AgentConfig
        {
            Type = "azure-openai", Model = "gpt-4.1",
            Deployment = "my-deployment"
        };
        var act = () => _sut.Create(config);
        act.Should().Throw<ConfigurationException>().WithMessage("*endpoint*");
    }

    [Fact]
    public void Create_UnknownType_ThrowsConfigurationException()
    {
        var config = new AgentConfig { Type = "unknown" };
        var act = () => _sut.Create(config);
        act.Should().Throw<ConfigurationException>()
            .WithMessage("*unknown*");
    }

    [Fact]
    public void Create_CaseInsensitive_Works()
    {
        var config = new AgentConfig { Type = "CLAUDE", Model = "test" };
        var provider = _sut.Create(config);
        provider.ProviderType.Should().Be("Claude");
    }
}
