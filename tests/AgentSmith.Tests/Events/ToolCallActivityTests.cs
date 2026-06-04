using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Events;

public sealed class ToolCallActivityTests
{
    [Fact]
    public async Task Backend_EmitsToolCallActivity_WithIntentAndTarget()
    {
        var recorder = EventTestStubs.Recording();
        var scope = new CallScope("coding-agent-master", "Execute", null)
        {
            Intent = "Reading Program.cs to confirm the entrypoint.",
        };
        var ctx = new FixedScopeRunContext("run-1", scope);
        var inner = AIFunctionFactory.Create((string path) => "ok", "read_file");
        var fn = new EventPublishingAIFunction(inner, recorder, ctx);

        await fn.InvokeAsync(new AIFunctionArguments { ["path"] = "src/Program.cs" }, CancellationToken.None);

        var call = recorder.Events.OfType<ToolCallEvent>().Single();
        call.Tool.Should().Be("read_file");                 // the action verb
        call.Summary.Should().Be("src/Program.cs");          // the target
        call.Intent.Should().Be("Reading Program.cs to confirm the entrypoint.");
    }

    [Fact]
    public async Task EventPublishingChatClient_CapturesIntentFromAssistantText_OntoScope()
    {
        var ctx = new ScopedRunContext("run-2");
        ctx.BeginCallScope("coding-agent-master", "Execute");
        var client = new EventPublishingChatClient(
            new StubChat("Editing Foo.cs to add the guard clause.\nThen I'll run the tests."),
            EventTestStubs.NoOp, ctx, EmptyPricing());

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "go") }, options: null, CancellationToken.None);

        ctx.CurrentCallScope!.Intent.Should().Be("Editing Foo.cs to add the guard clause.");
    }

    private static IModelPricingResolver EmptyPricing()
        => new ModelPricingResolver(new Dictionary<string, ModelPricing>(StringComparer.OrdinalIgnoreCase));

    private sealed class FixedScopeRunContext(string runId, CallScope scope) : IRunContextAccessor
    {
        public string? CurrentRunId => runId;
        public CallScope? CurrentCallScope => scope;
        public IDisposable BeginScope(string id) => new NoOp();
        public IDisposable BeginCallScope(string role, string phase, string? repoName = null) => new NoOp();
        private sealed class NoOp : IDisposable { public void Dispose() { } }
    }

    private sealed class ScopedRunContext(string runId) : IRunContextAccessor
    {
        private CallScope? _scope;
        public string? CurrentRunId => runId;
        public CallScope? CurrentCallScope => _scope;
        public IDisposable BeginScope(string id) => new NoOp();
        public IDisposable BeginCallScope(string role, string phase, string? repoName = null)
        {
            _scope = new CallScope(role, phase, repoName);
            return new NoOp();
        }
        private sealed class NoOp : IDisposable { public void Dispose() { } }
    }

    private sealed class StubChat(string assistantText) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, assistantText))
            {
                ModelId = "test-model",
            });

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
