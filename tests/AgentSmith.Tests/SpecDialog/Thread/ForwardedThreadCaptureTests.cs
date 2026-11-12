using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Providers;
using AgentSmith.Tests.Factories;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog.Thread;

public sealed class ForwardedThreadCaptureTests
{
    [Fact]
    public void ForwardedThreadCapture_TwoCalls_KeepsTheLastViewWithoutSystemPlusItsResponse()
    {
        var capture = new ForwardedThreadCapture();
        var prompt = new ChatMessage(ChatRole.User, "the prompt");
        var call = new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("c1", "read_file")]);
        var result = new ChatMessage(ChatRole.Tool, [new FunctionResultContent("c1", "file text")]);
        var answer = new ChatMessage(ChatRole.Assistant, "the answer");

        capture.Record([new(ChatRole.System, "system"), prompt], new ChatResponse(call));
        capture.Record([new(ChatRole.System, "system"), prompt, call, result], new ChatResponse(answer));

        capture.Messages.Should().Equal(prompt, call, result, answer);
    }

    [Fact]
    public void ForwardedThreadCapture_ResponseWithConversationId_IsUnusable()
    {
        var capture = new ForwardedThreadCapture();
        var prompt = new ChatMessage(ChatRole.User, "the prompt");

        capture.Record([prompt], new ChatResponse(new ChatMessage(ChatRole.Assistant, "first")) { ConversationId = "s-1" });
        capture.Record([prompt], new ChatResponse(new ChatMessage(ChatRole.Assistant, "second")));

        capture.Messages.Should().BeNull("a transport that keeps its own session forwarded no thread");
    }

    [Fact]
    public void ForwardedThreadCapture_Observe_OffersEachUsableThreadLedByTheLoopsMaster()
    {
        var capture = new ForwardedThreadCapture();
        var sink = new RecordingSink();
        var prompt = new ChatMessage(ChatRole.User, "the prompt");
        capture.Observe(sink);
        capture.Lead(true);

        capture.Record([new(ChatRole.System, "system"), prompt], new ChatResponse(new ChatMessage(ChatRole.Assistant, "first")));
        capture.Record([prompt], new ChatResponse(new ChatMessage(ChatRole.Assistant, "second")) { ConversationId = "s-1" });

        sink.Offers.Should().ContainSingle();
        sink.Offers[0].Messages.Select(m => m.Text).Should().Equal("the prompt", "first");
        sink.Offers[0].Stewarded.Should().BeTrue();
    }

    // 2026-11-12-a1fa: a turn handed over with a tool result is delivered once a later call carried it, never before.
    [Fact]
    public void ForwardedThreadCapture_Drained_DeliveredOnlyWithTheNextRecordedCall()
    {
        var capture = new ForwardedThreadCapture();
        var sink = new RecordingSink();
        var prompt = new ChatMessage(ChatRole.User, "the prompt");
        capture.Observe(sink);
        capture.Record([prompt], new ChatResponse(new ChatMessage(ChatRole.Assistant, "a tool call")));

        capture.Drained(4);
        capture.Delivered.Should().BeEmpty("the call that carries it has not answered yet");
        capture.Record([prompt], new ChatResponse(new ChatMessage(ChatRole.Assistant, "the answer")));

        capture.Delivered.Should().Equal(4);
        sink.Offers.Select(o => o.Delivered.Count).Should().Equal(0, 1);
    }

    [Fact]
    public async Task AgenticLoopRunner_RequestWithCapture_RecordsTheResponseText()
    {
        var capture = new ForwardedThreadCapture();
        var provider = new ToolCallingProvider(calls: 1);
        var runner = new AgenticLoopRunner(
            ToolLoopErrorToleranceSupport.LiveFactory(provider), EventTestStubs.RunContext, NullLogger<AgenticLoopRunner>.Instance);

        await runner.RunAsync(new AgenticLoopRequest(
            ToolLoopErrorToleranceSupport.Agent(), TaskType.Primary, "system", "do the work",
            [ToolLoopErrorToleranceSupport.Tool(_ => "file text")], ThreadCapture: capture), CancellationToken.None);

        capture.AnsweredText.Should().Be(ToolLoopErrorToleranceSupport.Answer);
        capture.Messages!.Select(m => m.Role).Should().Equal(ChatRole.User, ChatRole.Assistant, ChatRole.Tool, ChatRole.Assistant);
        capture.Messages![2].Contents.OfType<FunctionResultContent>().Single().Result.Should().Be("file text");
    }

    private sealed class RecordingSink : IForwardedThreadSink
    {
        public List<(IReadOnlyList<ChatMessage> Messages, bool Stewarded, IReadOnlyList<int> Delivered)> Offers { get; } = [];

        public void Offer(IReadOnlyList<ChatMessage> messages, bool stewarded, IReadOnlyList<int> delivered) =>
            Offers.Add((messages, stewarded, delivered));
    }
}
