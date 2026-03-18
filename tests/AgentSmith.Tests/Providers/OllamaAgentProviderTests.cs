using AgentSmith.Infrastructure.Services.Providers.Agent;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers;

public sealed class OllamaAgentProviderTests
{
    [Fact]
    public void ProviderType_IsOllama()
    {
        var sut = new OllamaAgentProvider(
            "qwen2.5-coder:32b", "http://localhost:11434",
            hasToolCalling: false, null, null,
            NullLogger<OllamaAgentProvider>.Instance);

        sut.ProviderType.Should().Be("ollama");
    }

    [Fact]
    public void Constructor_AcceptsToolCallingFlag()
    {
        var withTools = new OllamaAgentProvider(
            "qwen2.5-coder:32b", "http://localhost:11434",
            hasToolCalling: true, null, null,
            NullLogger<OllamaAgentProvider>.Instance);

        var withoutTools = new OllamaAgentProvider(
            "mistral-small:3.1", "http://localhost:11434",
            hasToolCalling: false, null, null,
            NullLogger<OllamaAgentProvider>.Instance);

        withTools.ProviderType.Should().Be("ollama");
        withoutTools.ProviderType.Should().Be("ollama");
    }
}
