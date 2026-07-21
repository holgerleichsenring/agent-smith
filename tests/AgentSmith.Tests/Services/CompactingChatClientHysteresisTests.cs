using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0362: WHEN does the compactor strike — and only then. The trigger measures the
/// compacted VIEW, not the raw append-only history; between fold events the forwarded
/// prefix is byte-stable so provider prompt caches hold, and the view order is
/// [head | summary | pin | tail] (cache-side-first). Operator-reported root: the
/// per-iteration regime above the threshold "pulled the context out from under" the
/// model every turn — this suite pins the exact firing behaviour.
/// </summary>
public sealed class CompactingChatClientHysteresisTests
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

    private sealed class Summarizer
    {
        public int Calls { get; private set; }

        public Task<string> Summarize(IReadOnlyList<ChatMessage> middle, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult($"SUMMARY#{Calls}");
        }
    }

    // Bodies are sized so the token estimate (~chars/4) is controllable per message.
    private static ChatMessage Msg(ChatRole role, string marker, int chars = 200) =>
        new(role, marker + new string('x', Math.Max(0, chars - marker.Length)));

    // History: system + initial user (the pinned head) + alternating rounds.
    private static List<ChatMessage> History(int rounds, int charsPerMessage = 200)
    {
        var list = new List<ChatMessage>
        {
            new(ChatRole.System, "SYSTEM"),
            Msg(ChatRole.User, "TICKET|", 120),
        };
        for (var i = 0; i < rounds; i++)
        {
            list.Add(Msg(ChatRole.Assistant, $"turn-{i}|", charsPerMessage));
            list.Add(Msg(ChatRole.User, $"result-{i}|", charsPerMessage));
        }
        return list;
    }

    private static string Text(ChatMessage m) =>
        string.Concat(m.Contents.OfType<TextContent>().Select(t => t.Text));

    private static MasterLoopHooks Hooks(Func<string?>? ledger = null) =>
        new(RenderLedgerForPin: ledger);

    // Trigger at 800 tokens (3200 chars): 10 rounds x 2 msgs x 200 chars ≈ 1030 tokens raw
    // crosses it; the post-fold view (~380 tokens) has ~4 rounds of headroom before a
    // re-crossing.
    private static CompactionConfig Config() => new()
    {
        IsEnabled = true, MaxContextTokens = 800, MaxContextTokensTriggerRatio = 1.0,
        KeepRecentIterations = 3,
    };

    [Fact]
    public async Task Compaction_Hysteresis_NoRefoldWhileViewUnderTrigger()
    {
        var inner = new RecordingInner();
        var sum = new Summarizer();
        var sut = new CompactingChatClient(inner, Config(), Hooks(() => "LEDGER"), sum.Summarize);

        // Below the trigger: nothing fires, raw forwarded.
        await sut.GetResponseAsync(History(rounds: 4));
        sum.Calls.Should().Be(0, "under the threshold the compactor must not strike");

        // Crossing: exactly ONE fold event.
        await sut.GetResponseAsync(History(rounds: 10));
        sum.Calls.Should().Be(1, "the first crossing folds once");

        // The raw history keeps growing but the compacted VIEW is small again —
        // no further summarizer calls. This is the regime that previously
        // compacted on every single call.
        await sut.GetResponseAsync(History(rounds: 11));
        await sut.GetResponseAsync(History(rounds: 12));
        await sut.GetResponseAsync(History(rounds: 13));
        sum.Calls.Should().Be(1, "between fold events the view is forwarded append-only");

        // The view still carries the summary + the growing tail.
        inner.Forwarded[^1].Should().Contain(m => Text(m).Contains("SUMMARY#1"));
        inner.Forwarded[^1].Should().Contain(m => Text(m).StartsWith("turn-12|"));
    }

    [Fact]
    public async Task Compaction_ViewRecrossing_TriggersRefold()
    {
        var inner = new RecordingInner();
        var sum = new Summarizer();
        var sut = new CompactingChatClient(inner, Config(), Hooks(), sum.Summarize);

        await sut.GetResponseAsync(History(rounds: 10));
        sum.Calls.Should().Be(1);

        // Grow the tail until the VIEW itself crosses the trigger again → second fold.
        await sut.GetResponseAsync(History(rounds: 24));
        sum.Calls.Should().Be(2, "a view re-crossing folds again — rarely, not per call");
    }

    [Fact]
    public async Task CompactedView_Order_HeadSummaryPinTail()
    {
        var inner = new RecordingInner();
        var sum = new Summarizer();
        var sut = new CompactingChatClient(inner, Config(), Hooks(() => "LEDGER-PIN"), sum.Summarize);

        await sut.GetResponseAsync(History(rounds: 10));

        var view = inner.Forwarded[^1];
        var ticket = view.FindIndex(m => Text(m).StartsWith("TICKET|"));
        var summary = view.FindIndex(m => Text(m).Contains("SUMMARY#1"));
        var pin = view.FindIndex(m => Text(m).Contains("LEDGER-PIN"));
        var firstTail = view.FindIndex(m => Text(m).StartsWith("turn-"));

        view[0].Role.Should().Be(ChatRole.System);
        ticket.Should().BePositive("the ticket rides in the pinned head");
        summary.Should().BeGreaterThan(ticket, "the big stable summary sits on the cache side");
        pin.Should().BeGreaterThan(summary, "the small volatile pin sits next to the tail");
        firstTail.Should().BeGreaterThan(pin);
        // Authority is semantic, not positional.
        Text(view[summary]).Should().Contain("authoritative");
        Text(view[pin]).Should().Contain("authoritative over the summary");
    }

    [Fact]
    public async Task CompactedView_PrefixByteStable_BetweenEvents_WhenLedgerUnchanged()
    {
        var inner = new RecordingInner();
        var sum = new Summarizer();
        var sut = new CompactingChatClient(inner, Config(), Hooks(() => "LEDGER-STABLE"), sum.Summarize);

        await sut.GetResponseAsync(History(rounds: 10));
        await sut.GetResponseAsync(History(rounds: 11));
        await sut.GetResponseAsync(History(rounds: 12));
        sum.Calls.Should().Be(1);

        // The prefix (head + summary + pin) must be byte-identical between calls —
        // this is what lets the provider prompt cache absorb the view.
        var a = inner.Forwarded[^2];
        var b = inner.Forwarded[^1];
        var prefixLength = a.FindIndex(m => Text(m).StartsWith("turn-"));
        prefixLength.Should().BePositive();
        for (var i = 0; i < prefixLength; i++)
            Text(b[i]).Should().Be(Text(a[i]), $"prefix message {i} must stay byte-stable");
        b.Count.Should().BeGreaterThan(a.Count, "the tail grows append-only between events");
    }

    [Fact]
    public async Task GovernorAboveCompactor_LedgerReminder_SurvivesIntoCompactedTail()
    {
        // p0362 + operator concern: the todo/checkmark forcing must keep working when
        // compaction is active. Chain governor -> compactor exactly like ChatClientFactory
        // does; the governor's staleness reminder is appended transiently at the END of
        // the raw list, so it must ride in the verbatim tail of the compacted view —
        // never be folded into the summary, never break the fold watermark.
        var inner = new RecordingInner();
        var sum = new Summarizer();
        var hooks = new MasterLoopHooks(
            RenderLedgerForPin: () => "LEDGER-PIN",
            RenderReminder: () => "REMINDER: update_progress checkmarks",
            ReminderEveryNIterations: 1,
            Compaction: Config());
        IChatClient chain = new CompactingChatClient(inner, Config(), hooks, sum.Summarize);
        chain = new MasterLoopGovernorChatClient(chain, hooks);

        await chain.GetResponseAsync(History(rounds: 10));

        var view = inner.Forwarded[^1];
        var reminder = view.FindIndex(m => Text(m).Contains("REMINDER: update_progress"));
        reminder.Should().Be(view.Count - 1, "the reminder is the trailing message of the tail");
        Text(view[^1]).Should().NotContain("SUMMARY#", "the reminder must never be folded away");
    }
}
