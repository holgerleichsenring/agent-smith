using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Services;

// p0341c: the master loop's in-pass governor — the within-pass money fence + the periodic
// ledger-reminder injection, both re-entered on every tool iteration (it sits below
// UseFunctionInvocation).
public sealed class MasterLoopGovernorChatClientTests
{
    // A fake inner client that records the messages it was forwarded per call.
    private sealed class RecordingInner : IChatClient
    {
        public List<List<ChatMessage>> Forwarded { get; } = new();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Forwarded.Add(messages.ToList());
            var resp = new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))
            {
                Usage = new UsageDetails { InputTokenCount = 100, OutputTokenCount = 50 },
            };
            return Task.FromResult(resp);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private static List<ChatMessage> Convo() => new()
    {
        new ChatMessage(ChatRole.System, "sys"),
        new ChatMessage(ChatRole.User, "task"),
    };

    [Fact]
    public async Task Budget_ToolIterationMiddleware_CancelsRunawaySinglePass()
    {
        var exhausted = false;
        var hooks = new MasterLoopHooks(
            IsBudgetExhausted: () => exhausted,
            RecordIterationUsage: _ => exhausted = true, // one iteration then the cap trips
            RenderReminder: () => null,
            ReminderEveryNIterations: 0, DriftEditlessIterations: 0);
        var sut = new MasterLoopGovernorChatClient(new RecordingInner(), hooks);

        // First iteration passes (budget not yet exhausted), records usage → exhausted.
        await sut.GetResponseAsync(Convo());
        // Second iteration: the fence trips BEFORE forwarding.
        var act = async () => await sut.GetResponseAsync(Convo());

        await act.Should().ThrowAsync<MasterBudgetExhaustedException>();
    }

    [Fact]
    public async Task InPassReminder_InjectedEveryNIterations_CarriesCurrentLedger()
    {
        var inner = new RecordingInner();
        var hooks = new MasterLoopHooks(
            RenderReminder: () => "[reminder] current ledger here",
            ReminderEveryNIterations: 2, DriftEditlessIterations: 0);
        var sut = new MasterLoopGovernorChatClient(inner, hooks);

        await sut.GetResponseAsync(Convo()); // iteration 1 — no injection
        await sut.GetResponseAsync(Convo()); // iteration 2 — injected

        inner.Forwarded[0].Should().NotContain(m => Text(m).Contains("[reminder]"));
        inner.Forwarded[1].Should().Contain(m => Text(m).Contains("current ledger here"));
    }

    [Fact]
    public async Task InPassReminder_DriftSignalNoEditsForKIterations_TriggersInjection()
    {
        var inner = new RecordingInner();
        var hooks = new MasterLoopHooks(
            RenderReminder: () => "[reminder] drift",
            ReminderEveryNIterations: 0, // periodic disabled — only drift can fire it
            DriftEditlessIterations: 2);
        var sut = new MasterLoopGovernorChatClient(inner, hooks);

        // Each convo ends with an assistant turn that called a READ tool (no edit) → drift grows.
        var readConvo = new List<ChatMessage>
        {
            new(ChatRole.System, "sys"),
            new(ChatRole.Assistant, new AIContent[] { new FunctionCallContent("c1", "read_file") }),
        };

        await sut.GetResponseAsync(readConvo); // streak 1
        await sut.GetResponseAsync(readConvo); // streak 2 → drift fires
        inner.Forwarded[1].Should().Contain(m => Text(m).Contains("drift"));
    }

    [Fact]
    public async Task Drift_ResetsWhenEditToolCalled_NoInjection()
    {
        var inner = new RecordingInner();
        var hooks = new MasterLoopHooks(
            RenderReminder: () => "[reminder] drift",
            ReminderEveryNIterations: 0, DriftEditlessIterations: 2);
        var sut = new MasterLoopGovernorChatClient(inner, hooks);

        var editConvo = new List<ChatMessage>
        {
            new(ChatRole.System, "sys"),
            new(ChatRole.Assistant, new AIContent[] { new FunctionCallContent("c1", "write_file") }),
        };

        await sut.GetResponseAsync(editConvo);
        await sut.GetResponseAsync(editConvo);
        inner.Forwarded.Should().OnlyContain(msgs => !msgs.Any(m => Text(m).Contains("drift")));
    }

    private static string Text(ChatMessage m) =>
        string.Concat(m.Contents.OfType<TextContent>().Select(t => t.Text));
}
