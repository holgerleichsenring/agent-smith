using AgentSmith.Application.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Loop;

public sealed class SkillCallOutcomeTests
{
    [Fact]
    public async Task ExecuteAsync_ValidJsonOutput_ReturnsOk()
    {
        var chat = new ScriptedRuntimeChatClient(() => ScriptedRuntimeChatClient.Make("{\"ok\":true}"));
        var (runtime, tracker, _) = RuntimeBuilder.Build(chat);

        var result = await runtime.ExecuteAsync(RuntimeBuilder.MakeRequest(), tracker, CancellationToken.None);

        result.Outcome.Should().Be(SkillCallOutcome.Ok);
        result.Output.Should().Be("{\"ok\":true}");
    }

    [Fact]
    public async Task ExecuteAsync_InvalidJson_RetriesOnce_ThenFailedParse()
    {
        var chat = new ScriptedRuntimeChatClient(
            () => ScriptedRuntimeChatClient.Make("garbage1"),
            () => ScriptedRuntimeChatClient.Make("still garbage"));
        var (runtime, tracker, _) = RuntimeBuilder.Build(chat);

        var result = await runtime.ExecuteAsync(RuntimeBuilder.MakeRequest(), tracker, CancellationToken.None);

        result.Outcome.Should().Be(SkillCallOutcome.FailedParse);
        chat.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ToolCrash_ReturnsFailedRuntime()
    {
        var chat = new ScriptedRuntimeChatClient(
            () => throw new InvalidOperationException("simulated crash"));
        var (runtime, tracker, _) = RuntimeBuilder.Build(chat);

        var result = await runtime.ExecuteAsync(RuntimeBuilder.MakeRequest(), tracker, CancellationToken.None);

        result.Outcome.Should().Be(SkillCallOutcome.FailedRuntime);
        result.FailureReason.Should().Contain("simulated crash");
    }

    [Fact]
    public async Task ExecuteAsync_NetworkException_ReturnsFailedRuntime()
    {
        var chat = new ScriptedRuntimeChatClient(
            () => throw new HttpRequestException("network down"));
        var (runtime, tracker, _) = RuntimeBuilder.Build(chat);

        var result = await runtime.ExecuteAsync(RuntimeBuilder.MakeRequest(), tracker, CancellationToken.None);

        result.Outcome.Should().Be(SkillCallOutcome.FailedRuntime);
    }
}
