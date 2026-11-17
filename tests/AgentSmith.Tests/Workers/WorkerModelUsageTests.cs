using AgentSmith.Infrastructure.Services.Workers;
using FluentAssertions;

namespace AgentSmith.Tests.Workers;

/// <summary>
/// 2026-11-17-7c9bf: a worker call records the model its CLI session ran; the model the CLI used on the side
/// (Claude Code asks haiku for housekeeping) is secondary usage, and the call's cost stays the CLI's total.
/// </summary>
public sealed class WorkerModelUsageTests
{
    private const string Result =
        """{"type":"result","subtype":"success","is_error":false,"num_turns":1,"result":"The guard exists.","total_cost_usd":0.4213,"usage":{"input_tokens":2,"output_tokens":900,"cache_read_input_tokens":12149,"cache_creation_input_tokens":57017},"modelUsage":{"claude-haiku-4-5-20251001":{"inputTokens":412,"outputTokens":20,"cacheReadInputTokens":0,"cacheCreationInputTokens":0,"costUSD":0.0005,"contextWindow":200000},"claude-opus-5-5":{"inputTokens":2,"outputTokens":900,"cacheReadInputTokens":12149,"cacheCreationInputTokens":57017,"costUSD":0.4208,"contextWindow":200000}}}""";

    [Fact]
    public void Stream_InitEventAfterAHookEvent_NamesThePrimaryAndKeepsHaikuAsSecondary()
    {
        var stream = string.Join("\n",
            """{"type":"system","subtype":"hook_response","hook_name":"SessionStart"}""",
            """{"type":"system","subtype":"init","session_id":"s","model":"claude-opus-5-5","tools":[]}""",
            """{"type":"assistant","message":{"content":[{"type":"text","text":"The guard exists."}]}}""",
            Result);

        WorkerCliEnvelope.TryRead(stream, out var envelope).Should().BeTrue();

        var accounting = envelope.Accounting;
        accounting.Model.Should().Be("claude-opus-5-5", "the session ran opus; haiku is not part of the name");
        accounting.ReportedCostUsd.Should().Be(0.4213m, "the call's cost stays the CLI's total");
        accounting.OutputTokens.Should().Be(900);
        accounting.Secondary.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Model = "claude-haiku-4-5-20251001", InputTokens = 412L, OutputTokens = 20L, CostUsd = 0.0005m,
        });
    }

    [Fact]
    public void Json_NoInitEvent_ThePrimaryIsTheModelThatWroteMost()
    {
        WorkerCliEnvelope.TryRead(Result, out var envelope).Should().BeTrue();

        envelope.Accounting.Model.Should().Be("claude-opus-5-5");
        envelope.Accounting.Secondary.Select(u => u.Model).Should().Equal("claude-haiku-4-5-20251001");
    }

    [Fact]
    public void Stream_InitModelWithASuffixModelUsageDoesNotName_FallsBackToMostOutput()
    {
        var stream = string.Join("\n",
            """{"type":"system","subtype":"init","session_id":"s","model":"claude-opus-5-5[1m]"}""", Result);

        WorkerCliEnvelope.TryRead(stream, out var envelope).Should().BeTrue();

        envelope.Accounting.Model.Should().Be("claude-opus-5-5");
    }
}
