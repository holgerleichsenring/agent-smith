using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Measurement;

/// <summary>2026-11-25-9992k: a question an agent asks through the one loop every agent runs on is a recorded tool call —
/// the fact the run's question count folds — and no other tool is wrapped.</summary>
public sealed class AskHumanPublishingTests
{
    [Fact]
    public async Task AskHuman_CalledInTheLoop_PublishesOneToolCallNamingIt()
    {
        var publisher = new RecordingEventPublisher();
        var runContext = new AsyncLocalRunContextAccessor();
        var runner = new AgenticLoopRunner(Factory(out var model), runContext, NullLogger<AgenticLoopRunner>.Instance, publisher);
        model.Calls = ["ask_human", "read_file"];

        using (runContext.BeginScope("run-1"))
            await runner.RunAsync(Request(), CancellationToken.None);

        publisher.Events.OfType<ToolCallEvent>().Select(e => (e.RunId, e.Tool)).Should().Equal([("run-1", "ask_human")]);
        model.Invoked.Should().Equal(["asked", "read"], "the wrapped tool still answers");
    }

    [Fact]
    public async Task WithoutAPublisher_TheToolsAreHandedOverAsTheyAre()
    {
        var runner = Runner(null, out var model);

        await runner.RunAsync(Request(), CancellationToken.None);

        model.SeenTools.Should().AllSatisfy(t => t.Should().NotBeOfType<EventPublishingAIFunction>());
    }

    private static AgenticLoopRunner Runner(IEventPublisher? publisher, out ScriptedModel model) =>
        new(Factory(out model), new AsyncLocalRunContextAccessor(), NullLogger<AgenticLoopRunner>.Instance, publisher);

    private static IChatClientFactory Factory(out ScriptedModel model)
    {
        var scripted = new ScriptedModel();
        model = scripted;
        var factory = new Mock<IChatClientFactory>();
        factory.Setup(f => f.Create(It.IsAny<AgentConfig>(), It.IsAny<TaskType>(), It.IsAny<int?>(), It.IsAny<MasterLoopHooks?>(),
            It.IsAny<CompactionConfig?>(), It.IsAny<int?>(), It.IsAny<ForwardedThreadCapture?>())).Returns(scripted);
        factory.Setup(f => f.GetMaxOutputTokens(It.IsAny<AgentConfig>(), It.IsAny<TaskType>())).Returns(100);
        return factory.Object;
    }

    private static AgenticLoopRequest Request() => new(new AgentConfig(), TaskType.Primary, "system", "user",
    [
        AIFunctionFactory.Create(() => "asked", "ask_human"),
        AIFunctionFactory.Create(() => "read", "read_file"),
    ]);

    /// <summary>Invokes the named tools once each, as a tool loop would, and answers.</summary>
    private sealed class ScriptedModel : IChatClient
    {
        public string[] Calls { get; set; } = [];
        public List<string> Invoked { get; } = [];
        public List<AITool> SeenTools { get; } = [];

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            SeenTools.AddRange(options?.Tools ?? []);
            foreach (var name in Calls)
            {
                var tool = options!.Tools!.OfType<AIFunction>().Single(t => t.Name == name);
                Invoked.Add((string)((System.Text.Json.JsonElement)(await tool.InvokeAsync(new AIFunctionArguments(), cancellationToken))!).GetString()!);
            }
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
