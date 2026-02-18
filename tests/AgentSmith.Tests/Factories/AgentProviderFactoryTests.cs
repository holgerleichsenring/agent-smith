using AgentSmith.Contracts.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Configuration;
using AgentSmith.Infrastructure.Factories;
using AgentSmith.Infrastructure.Providers.Agent;
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
        _sut = new AgentProviderFactory(_secrets, NullLoggerFactory.Instance);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
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
