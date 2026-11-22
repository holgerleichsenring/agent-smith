using AgentSmith.Contracts.Services;
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

        var textEstimate = MessageTokenEstimate.Of(textOnly);
        var toolEstimate = MessageTokenEstimate.Of(withToolResult);

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

        MessageTokenEstimate.Of(messages)
            .Should().BeGreaterThan(400, "the 2000-char command argument must be counted");
    }

    // p0350: a single call larger than the whole per-minute token bucket must be
    // THROTTLED (clamped to capacity, waits for a full bucket), not turned into a
    // hard ArgumentOutOfRangeException that kills a run mid-flight. The bucket
    // starts full, so clamping to capacity lets the call proceed immediately.
    [Fact]
    public async Task AcquireAsync_SingleCallLargerThanBucket_ThrottlesInsteadOfThrowing()
    {
        var limiter = new LlmRateLimiter(new LlmRateLimitOptions(RequestsPerMinute: 60, InputTokensPerMinute: 1000));

        var acquire = async () =>
        {
            await limiter.AcquireAsync(new LlmQuotaDemand(5000, 0), CancellationToken.None);
        };

        await acquire.Should().NotThrowAsync(
            "an over-budget single call must wait for the bucket, never crash the run");
    }

    // 2026-11-22-66e4a: a sub-agent's first turn is mostly its tool schemas, which are sent with every call.
    [Fact]
    public void EstimateInputTokens_IncludesTheToolDefinitions()
    {
        var messages = new[] { new ChatMessage(ChatRole.User, "hi") };
        var tool = AIFunctionFactory.Create((string path) => path, "read_file", new string('d', 4000));

        MessageTokenEstimate.Of(messages, new ChatOptions { Tools = [tool] })
            .Should().BeGreaterThan(MessageTokenEstimate.Of(messages) + 900, "the 4000-char tool description is sent too");
    }
}
