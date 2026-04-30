using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Providers.Agent.Adapters;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Factories;

public class AgenticAnalyzerFactoryTests : IDisposable
{
    private readonly SecretsProvider _secrets = new();
    private readonly AgenticAnalyzerFactory _sut;

    public AgenticAnalyzerFactoryTests()
    {
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "test-key");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", "test-key");
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", "test-key");
        _sut = new AgenticAnalyzerFactory(_secrets, NullLoggerFactory.Instance);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", null);
    }

    [Theory]
    [InlineData("claude", typeof(ClaudeAgenticAnalyzer))]
    [InlineData("anthropic", typeof(ClaudeAgenticAnalyzer))]
    [InlineData("openai", typeof(OpenAiAgenticAnalyzer))]
    [InlineData("gemini", typeof(GeminiAgenticAnalyzer))]
    [InlineData("google", typeof(GeminiAgenticAnalyzer))]
    public void Create_ValidType_ReturnsCorrectAdapter(string type, Type expected)
    {
        var config = new AgentConfig { Type = type, Model = "test-model" };
        var analyzer = _sut.Create(config);
        analyzer.Should().BeOfType(expected);
    }

    [Theory]
    [InlineData("azure-openai")]
    [InlineData("azure")]
    public void Create_AzureOpenAi_ReturnsAzureAdapter(string type)
    {
        var config = new AgentConfig
        {
            Type = type,
            Model = "gpt-4.1",
            Deployment = "gpt4-1-deployment",
            Endpoint = "https://test.openai.azure.com",
            ApiVersion = "2025-01-01-preview"
        };
        var analyzer = _sut.Create(config);
        analyzer.Should().BeOfType<AzureOpenAiAgenticAnalyzer>();
    }

    [Fact]
    public void Create_OllamaType_ThrowsNotSupportedWithDeferralMessage()
    {
        var config = new AgentConfig { Type = "ollama", Model = "qwen2.5-coder" };

        var act = () => _sut.Create(config);

        var ex = act.Should().Throw<NotSupportedException>().Which;
        ex.Message.Should().Contain("Ollama");
        ex.Message.Should().Contain("p0110a");
        ex.Message.Should().Contain("docs/configuration/agents.md");
    }

    [Fact]
    public void Create_UnknownType_ThrowsConfigurationException()
    {
        var config = new AgentConfig { Type = "mystery-llm", Model = "x" };

        var act = () => _sut.Create(config);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*mystery-llm*");
    }

    [Fact]
    public void Create_AzureOpenAiWithoutEndpoint_ThrowsConfigurationException()
    {
        var config = new AgentConfig { Type = "azure-openai", Model = "gpt-4.1" };

        var act = () => _sut.Create(config);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*endpoint*");
    }
}
