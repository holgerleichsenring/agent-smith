using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Agent.Compaction;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Chat;

namespace AgentSmith.Tests.Compaction;

public sealed class OpenAiContextCompactorTests
{
    private static readonly CompactionConfig EnabledConfig = new()
    {
        IsEnabled = true, ThresholdIterations = 8, MaxContextTokens = 80_000
    };

    [Fact]
    public async Task CompactIfNeededAsync_DisabledConfig_ReturnsInputUnchangedNullEvent()
    {
        var disabled = new CompactionConfig { IsEnabled = false };
        var sut = NewSut(disabled);
        var messages = TwoMessages();

        var result = await sut.CompactIfNeededAsync(messages, currentIterations: 100, estimatedAccumulatedTokens: 999_999, CancellationToken.None);

        result.Messages.Should().BeSameAs(messages);
        result.Event.Should().BeNull();
    }

    [Fact]
    public async Task CompactIfNeededAsync_BelowBothThresholds_ReturnsInputUnchanged()
    {
        var sut = NewSut(EnabledConfig);
        var messages = TwoMessages();

        var result = await sut.CompactIfNeededAsync(messages, currentIterations: 1, estimatedAccumulatedTokens: 100, CancellationToken.None);

        result.Messages.Should().BeSameAs(messages);
        result.Event.Should().BeNull();
    }

    [Fact]
    public async Task CompactIfNeededAsync_TriggerCrossedButTinyMessageList_ReturnsInputUnchanged()
    {
        // Iteration trigger crosses but only system + user are present — nothing to compact.
        var sut = NewSut(EnabledConfig);
        var messages = TwoMessages();

        var result = await sut.CompactIfNeededAsync(messages, currentIterations: 100, estimatedAccumulatedTokens: 100, CancellationToken.None);

        result.Messages.Should().BeSameAs(messages);
        result.Event.Should().BeNull();
    }

    [Fact]
    public void EstimateTokens_EmptyList_ReturnsZero()
    {
        OpenAiContextCompactor.EstimateTokens(Array.Empty<ChatMessage>()).Should().Be(0);
    }

    [Fact]
    public void EstimateTokens_KnownText_RoundsToCharsPerTokenHeuristic()
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("0123456789AB") // 12 chars → 12/4 = 3 tokens
        };
        OpenAiContextCompactor.EstimateTokens(messages).Should().Be(3);
    }

    [Fact]
    public async Task CompactIfNeededAsync_NoOpVariant_ReturnsInputUnchanged()
    {
        var sut = new NoOpOpenAiContextCompactor();

        var result = await sut.CompactIfNeededAsync(TwoMessages(), 999, 999_999, CancellationToken.None);

        result.Messages.Should().NotBeNull();
        result.Event.Should().BeNull();
    }

    private static List<ChatMessage> TwoMessages() => new()
    {
        new SystemChatMessage("system"),
        new UserChatMessage("hi")
    };

    private static OpenAiContextCompactor NewSut(CompactionConfig config) =>
        new(
            summarizerClient: new ChatClient("gpt-4o-mini", "test-key"),
            config: config,
            prompts: new FakePromptCatalog().WithPrompt("openai-context-compactor-system", "summarize"),
            logger: NullLogger<OpenAiContextCompactor>.Instance);
}
