using Microsoft.Extensions.AI;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// What a tool loop last forwarded to its provider, without the leading system messages, followed by what the
/// provider answered: the thread a later turn of the same conversation replays. The last call wins, so a loop that
/// folded keeps its folded view. A transport that keeps its own session names a ConversationId on its responses and
/// forwards only the new messages, so what it was sent is no thread and the capture holds none.
/// <para>2026-11-12-92e9a: each usable thread is also offered to the sink the turn observes it with, led by the master
/// the loop started with.</para>
/// </summary>
public sealed class ForwardedThreadCapture
{
    private IReadOnlyList<ChatMessage>? _messages;
    private bool _unusable;
    private IForwardedThreadSink? _sink;
    private bool _stewarded;

    /// <summary>The captured thread, or null when nothing was captured or the transport keeps its own session.</summary>
    public IReadOnlyList<ChatMessage>? Messages => _unusable ? null : _messages;

    /// <summary>The response text the loop's last pass reported.</summary>
    public string? AnsweredText { get; private set; }

    public void Record(IReadOnlyList<ChatMessage> sent, ChatResponse received)
    {
        ArgumentNullException.ThrowIfNull(sent);
        ArgumentNullException.ThrowIfNull(received);
        if (received.ConversationId is not null) _unusable = true;
        _messages = [.. sent.SkipWhile(m => m.Role == ChatRole.System), .. received.Messages];
        if (!_unusable) _sink?.Offer(_messages, _stewarded);
    }

    /// <summary>Hands every thread recorded from now on to <paramref name="sink"/>.</summary>
    public void Observe(IForwardedThreadSink sink) => _sink = sink ?? throw new ArgumentNullException(nameof(sink));

    /// <summary>Set at loop start: whether the ticket steward leads the loop whose threads are offered.</summary>
    public void Lead(bool stewarded) => _stewarded = stewarded;

    public void Answered(string? text) => AnsweredText = text;
}
