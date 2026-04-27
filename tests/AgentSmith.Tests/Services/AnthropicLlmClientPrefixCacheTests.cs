using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services;
using Anthropic.SDK.Messaging;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class AnthropicLlmClientPrefixCacheTests
{
    [Fact]
    public void CompleteWithCachedPrefix_EmitsCacheControlMarker()
    {
        var msg = AnthropicLlmClient.BuildCachedPrefixUserMessage("prefix block", "suffix block");
        msg.Content.Should().HaveCount(2);
        var first = msg.Content[0].Should().BeOfType<TextContent>().Subject;
        first.Text.Should().Be("prefix block");
        first.CacheControl.Should().NotBeNull();
        first.CacheControl!.Type.Should().Be(CacheControlType.ephemeral);
        var second = msg.Content[1].Should().BeOfType<TextContent>().Subject;
        second.Text.Should().Be("suffix block");
        second.CacheControl.Should().BeNull();
    }

    [Fact]
    public void CompleteWithCachedPrefix_EmptySuffix_OnlyPrefixBlock()
    {
        var msg = AnthropicLlmClient.BuildCachedPrefixUserMessage("only prefix", "");
        msg.Content.Should().HaveCount(1);
        msg.Content[0].Should().BeOfType<TextContent>()
            .Which.CacheControl!.Type.Should().Be(CacheControlType.ephemeral);
    }

    [Fact]
    public async Task CompleteWithCachedPrefix_DefaultImpl_FallsBackToCompleteAsync()
    {
        var mock = new Mock<ILlmClient> { CallBase = false };
        mock.Setup(c => c.CompleteAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<TaskType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse("ok", 10, 5, "test-model"));

        ILlmClient client = new FallthroughClient(mock.Object);
        var response = await client.CompleteWithCachedPrefixAsync(
            "sys", "prefix", "suffix", TaskType.Planning, CancellationToken.None);

        response.Text.Should().Be("ok");
        mock.Verify(c => c.CompleteAsync(
            "sys", "prefix\n\nsuffix",
            TaskType.Planning, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void TokenUsageTracker_TracksCacheReadAndCacheCreateSeparately()
    {
        var tracker = new PipelineCostTracker();
        tracker.Track(new LlmResponse("a", 100, 50, "m", CacheCreationTokens: 200, CacheReadTokens: 0));
        tracker.Track(new LlmResponse("b", 80, 40, "m", CacheCreationTokens: 0, CacheReadTokens: 200));
        tracker.Track(new LlmResponse("c", 80, 40, "m", CacheCreationTokens: 0, CacheReadTokens: 200));

        tracker.TotalCacheCreateTokens.Should().Be(200);
        tracker.TotalCacheReadTokens.Should().Be(400);
        tracker.ToString().Should().Contain("400 read");
    }

    private sealed class FallthroughClient(ILlmClient inner) : ILlmClient
    {
        public Task<LlmResponse> CompleteAsync(
            string systemPrompt, string userPrompt,
            TaskType taskType, CancellationToken cancellationToken) =>
            inner.CompleteAsync(systemPrompt, userPrompt, taskType, cancellationToken);
    }
}
