using AgentSmith.Infrastructure.Services.Providers.Agent;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers;

public sealed class OllamaAgentProviderTests
{
    private static OpenAiCompatibleClient CreateClient() =>
        new("http://localhost:11434/v1", null, NullLogger.Instance);

    [Fact]
    public void ProviderType_IsOllama()
    {
        var sut = new OllamaAgentProvider(
            "qwen2.5-coder:32b", CreateClient(),
            hasToolCalling: false, null, null,
            NullLogger<OllamaAgentProvider>.Instance);

        sut.ProviderType.Should().Be("ollama");
    }

    [Fact]
    public void Constructor_AcceptsToolCallingFlags()
    {
        var withTools = new OllamaAgentProvider(
            "qwen2.5-coder:32b", CreateClient(),
            hasToolCalling: true, null, null,
            NullLogger<OllamaAgentProvider>.Instance);

        var withoutTools = new OllamaAgentProvider(
            "mistral-small:3.1", CreateClient(),
            hasToolCalling: false, null, null,
            NullLogger<OllamaAgentProvider>.Instance);

        withTools.ProviderType.Should().Be("ollama");
        withoutTools.ProviderType.Should().Be("ollama");
    }
}
