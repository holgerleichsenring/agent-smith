using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Services;

// p0341d: in-loop compaction as a provider-agnostic DelegatingChatClient. Once the message
// list crosses the threshold it forwards a REDUCED view — system + current ledger +
// working-state pin + recent tail verbatim + an incremental summary of the evicted middle —
// so a single master pass preserves the thread instead of dying at the raw context window.
public sealed class CompactingChatClientTests
{
    private sealed class RecordingInner : IChatClient
    {
        public List<List<ChatMessage>> Forwarded { get; } = new();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Forwarded.Add(messages.ToList());
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    // A recording summarizer — returns a marker + counts calls + captures each input.
    private sealed class Summarizer
    {
        public int Calls { get; private set; }
        public List<List<ChatMessage>> Inputs { get; } = new();

        public Task<string> Summarize(IReadOnlyList<ChatMessage> middle, CancellationToken ct)
        {
            Calls++;
            Inputs.Add(middle.ToList());
            return Task.FromResult($"SUMMARY#{Calls}");
        }
    }

    // Fires on iteration >= 1 (deterministic), keeps the last 6 messages.
    private static CompactionConfig AlwaysConfig() =>
        new() { IsEnabled = true, ThresholdIterations = 1, KeepRecentIterations = 3, MaxContextTokensTriggerRatio = 0 };

    // Never fires: iteration threshold high + token trigger disabled.
    private static CompactionConfig NeverConfig() =>
        new() { IsEnabled = true, ThresholdIterations = 1000, MaxContextTokensTriggerRatio = 0 };

    private static List<ChatMessage> LongConvo(int userMessages, string prefix = "m")
    {
        var list = new List<ChatMessage> { new(ChatRole.System, "SYSTEM PROMPT") };
        for (var i = 0; i < userMessages; i++)
            list.Add(new ChatMessage(
                i % 2 == 0 ? ChatRole.User : ChatRole.Assistant, $"{prefix}-{i}-body"));
        return list;
    }

    private static string Text(ChatMessage m) =>
        string.Concat(m.Contents.OfType<TextContent>().Select(t => t.Text));

    private static MasterLoopHooks Hooks(
        Func<string?>? ledger = null, Func<string?>? working = null, CompactionConfig? config = null) =>
        new(RenderLedgerForPin: ledger, RenderWorkingStateForPin: working, Compaction: config);

    [Fact]
    public async Task CompactingClient_UnderThreshold_ForwardsUntouched()
    {
        var inner = new RecordingInner();
        var sum = new Summarizer();
        var sut = new CompactingChatClient(inner, NeverConfig(), Hooks(), sum.Summarize);

        var convo = LongConvo(14);
        await sut.GetResponseAsync(convo);

        inner.Forwarded[0].Should().HaveCount(convo.Count, "under threshold => forwarded verbatim");
        sum.Calls.Should().Be(0);
    }

    [Fact]
    public async Task CompactingClient_OverThreshold_PinsSystemLedgerWorkingStateAndTail()
    {
        var inner = new RecordingInner();
        var sum = new Summarizer();
        var sut = new CompactingChatClient(
            inner, AlwaysConfig(),
            Hooks(ledger: () => "LEDGER-PIN", working: () => "WORKING-STATE-PIN"),
            sum.Summarize);

        var convo = LongConvo(14);
        await sut.GetResponseAsync(convo);

        var forwarded = inner.Forwarded[0];
        forwarded.Should().HaveCountLessThan(convo.Count, "the middle was folded into a summary");
        forwarded[0].Role.Should().Be(ChatRole.System);
        forwarded.Should().Contain(m => Text(m).Contains("LEDGER-PIN"));
        forwarded.Should().Contain(m => Text(m).Contains("WORKING-STATE-PIN"));
        forwarded.Should().Contain(m => Text(m).Contains("SUMMARY#1"));
        // The recent tail survives verbatim; the earliest middle body was evicted.
        forwarded.Should().Contain(m => Text(m).Contains("m-13-body"));
        forwarded.Should().NotContain(m => Text(m).Contains("m-2-body"));
    }

    [Fact]
    public async Task CompactingClient_LedgerRenderedFromPipelineContext_CurrentNotSnapshot()
    {
        var inner = new RecordingInner();
        var sum = new Summarizer();
        var ledgerState = "LEDGER-V1";
        var sut = new CompactingChatClient(
            inner, AlwaysConfig(), Hooks(ledger: () => ledgerState), sum.Summarize);

        await sut.GetResponseAsync(LongConvo(14));
        ledgerState = "LEDGER-V2"; // the live ledger advanced between compactions
        await sut.GetResponseAsync(LongConvo(20));

        inner.Forwarded[0].Should().Contain(m => Text(m).Contains("LEDGER-V1"));
        inner.Forwarded[1].Should().Contain(m => Text(m).Contains("LEDGER-V2"),
            "the pin renders the CURRENT ledger, not a pass-start snapshot");
    }

    [Fact]
    public async Task CompactingClient_SummaryIncremental_NoResummarizePerIteration()
    {
        var inner = new RecordingInner();
        var sum = new Summarizer();
        var sut = new CompactingChatClient(inner, AlwaysConfig(), Hooks(), sum.Summarize);

        await sut.GetResponseAsync(LongConvo(14, "a"));
        await sut.GetResponseAsync(LongConvo(30, "a")); // history grew — only the NEW middle folds

        sum.Calls.Should().Be(2);
        // The second summarization must NOT re-summarize the messages already folded by the
        // first (its input starts AFTER the first eviction watermark).
        var secondInput = sum.Inputs[1];
        secondInput.Should().NotContain(m => Text(m).Contains("a-0-body"),
            "already-summarized messages are not re-sent to the summarizer");
    }

    [Fact]
    public async Task CompactingClient_SummarizerFails_ForwardsFullHistory_FailOpen()
    {
        var inner = new RecordingInner();
        var sut = new CompactingChatClient(
            inner, AlwaysConfig(), Hooks(),
            (_, _) => throw new InvalidOperationException("summarizer down"));

        var convo = LongConvo(14);
        await sut.GetResponseAsync(convo);

        inner.Forwarded[0].Should().HaveCount(convo.Count, "a failed summarizer falls open to the full history");
    }

    [Fact]
    public async Task MasterPass_RunsPastRawWindowEquivalent_ThreadPreserved()
    {
        // Simulate a long pass: the history keeps growing and crosses the threshold every
        // iteration; the ledger pin + the recent tail survive each compaction (the thread is
        // preserved), and the provider never sees the unbounded raw history.
        var inner = new RecordingInner();
        var sum = new Summarizer();
        var sut = new CompactingChatClient(
            inner, AlwaysConfig(), Hooks(ledger: () => "PINNED-LEDGER"), sum.Summarize);

        for (var round = 1; round <= 5; round++)
            await sut.GetResponseAsync(LongConvo(10 + round * 6));

        inner.Forwarded.Should().OnlyContain(f => f.Any(m => Text(m).Contains("PINNED-LEDGER")));
        inner.Forwarded[^1].Count.Should().BeLessThan(10 + 5 * 6 + 1,
            "the provider sees a bounded, compacted view — not the raw window");
    }
}
