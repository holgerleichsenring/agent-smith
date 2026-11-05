using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Tests.Factories;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.SpecDialog.Thread;

public sealed class ChatClientFactoryDialogCaptureTests
{
    [Fact]
    public async Task ChatClientFactory_DialogCapture_RecordsTheFoldedViewBelowTheCompactor()
    {
        var provider = new ToolCallingProvider(calls: 0);
        var agent = ToolLoopErrorToleranceSupport.Agent();
        var capture = new ForwardedThreadCapture();
        var client = ToolLoopErrorToleranceSupport.LiveFactory(provider).Create(
            agent, TaskType.Primary, 100, null, agent.Compaction, contextWindowOverride: 4000, capture: capture);
        var sweep = new List<ChatMessage> { new(ChatRole.System, "SYSTEM"), new(ChatRole.User, "USER") };
        for (var i = 0; i < 20; i++) sweep.Add(new ChatMessage(ChatRole.Assistant, new string((char)('a' + i), 800)));

        await client.GetResponseAsync(sweep, new ChatOptions { Tools = [AIFunctionFactory.Create(() => "ok", "probe")] });

        var forwarded = provider.Requests[^1];
        forwarded.Should().Contain(m => m.Text.Contains("[Context summary"), "the loop folded");
        capture.Messages!.Select(m => m.Text).Should().Equal(
            [.. forwarded.Skip(1).Select(m => m.Text), ToolLoopErrorToleranceSupport.Answer],
            "the capture keeps the folded view the provider was sent, without the system message, plus its answer");
    }
}
