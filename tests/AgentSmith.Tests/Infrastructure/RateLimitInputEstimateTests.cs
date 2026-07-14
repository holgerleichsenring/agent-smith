using AgentSmith.Infrastructure.Services.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Infrastructure;

/// <summary>
/// Regression: an agentic loop re-sends the whole conversation each turn, and the
/// bulk is tool traffic (function calls + tool RESULTS). The old estimator counted
/// only TextContent, undercounting ~7x (25k estimated vs ~190k actual on a real
/// coding run), so the limiter throttled on a fiction while real load blew past
/// the provider quota. The estimate must include tool-call and tool-result content.
/// </summary>
public sealed class RateLimitInputEstimateTests
{
    [Fact]
    public void EstimateInputTokens_IncludesToolResults_NotJustText()
    {
        var textOnly = new[] { new ChatMessage(ChatRole.User, "hello") };
        var withToolResult = new[]
        {
            new ChatMessage(ChatRole.User, "hello"),
            new ChatMessage(ChatRole.Tool,
                new List<AIContent> { new FunctionResultContent("call-1", new string('x', 4000)) }),
        };

        var textEstimate = RateLimitingChatClient.EstimateInputTokens(textOnly);
        var toolEstimate = RateLimitingChatClient.EstimateInputTokens(withToolResult);

        // 4000 chars / 4 chars-per-token ≈ 1000 tokens the old estimator ignored.
        toolEstimate.Should().BeGreaterThan(textEstimate + 900,
            "a large tool result must count toward the input-token estimate");
    }

    [Fact]
    public void EstimateInputTokens_IncludesFunctionCallArguments()
    {
        var call = new FunctionCallContent("call-1", "run_command",
            new Dictionary<string, object?> { ["command"] = new string('y', 2000) });
        var messages = new[] { new ChatMessage(ChatRole.Assistant, new List<AIContent> { call }) };

        RateLimitingChatClient.EstimateInputTokens(messages)
            .Should().BeGreaterThan(400, "the 2000-char command argument must be counted");
    }
}
