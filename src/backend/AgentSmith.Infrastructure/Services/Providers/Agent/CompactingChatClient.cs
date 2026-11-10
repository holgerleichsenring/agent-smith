using System.Text;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// In-loop context compaction below UseFunctionInvocation, provider-agnostic. The function-invoking client re-sends
/// its growing list every iteration; once the forwarded VIEW crosses MaxContextTokens × ratio, the middle is folded
/// into an incremental summary and the watermark frozen, so between folds the view [head | summary | pin | tail]
/// grows append-only with a byte-stable prefix the provider cache holds. The pinned head is the system message(s)
/// plus the first user message, kept verbatim.
/// <para>
/// A loop that replays an earlier turn's thread starts with THAT turn's prompt as its first user message, so built
/// with <c>pinsCurrentPrompt</c> it also pins, by reference, the message its first call ended with — this turn's
/// prompt — while that instance is still in the list, placed straight after the head, where the summary's "the
/// ticket above" points at it.
/// </para>
/// </summary>
public sealed class CompactingChatClient : DelegatingChatClient
{
    private const int CharsPerTokenEstimate = 4;

    private readonly CompactionConfig _config;
    private readonly MasterLoopHooks? _hooks;
    private readonly Func<IReadOnlyList<ChatMessage>, CancellationToken, Task<string>> _summarize;
    private readonly ILogger? _logger;
    private readonly bool _pinsCurrentPrompt;
    private ChatMessage? _currentPrompt;

    // p0341d/p0362: the incremental summary + the absolute raw index up to which history
    // is folded into it. FIC hands us the full append-only history each call; we summarize
    // only [_foldedUpTo .. tailStart) at fold events and extend — never re-summarize.
    // _foldedUpTo == 0 means no fold event has happened yet.
    private string _summary = string.Empty;
    private int _foldedUpTo;
    private int _foldEvents;

    public CompactingChatClient(
        IChatClient inner,
        CompactionConfig config,
        MasterLoopHooks? hooks,
        Func<IReadOnlyList<ChatMessage>, CancellationToken, Task<string>> summarize,
        ILogger? logger = null,
        bool pinsCurrentPrompt = false)
        : base(inner)
    {
        _pinsCurrentPrompt = pinsCurrentPrompt;
        _config = config;
        _hooks = hooks;
        _summarize = summarize;
        _logger = logger;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var list = messages as IList<ChatMessage> ?? messages.ToList();
        if (_pinsCurrentPrompt && _currentPrompt is null && list.Count > 0) _currentPrompt = list[^1];

        // p0362: before the first fold the view IS the raw list; afterwards it is
        // [head | summary | pin | tail]. Either way the trigger measures what would
        // actually be sent — the raw history's size is irrelevant once it's folded.
        var view = BuildView(list);
        if (!ShouldCompactOnTokenPressure(EstimateTokens(view), _config))
            return await base.GetResponseAsync(view, options, cancellationToken);

        var compacted = await FoldAsync(list, cancellationToken);
        return await base.GetResponseAsync(compacted, options, cancellationToken);
    }

    /// <summary>
    /// p0357: the middleware's trigger predicate — token pressure only. The iteration
    /// count deliberately plays no part: config.ThresholdIterations is a deprecated
    /// no-op (see <see cref="CompactionConfig.ThresholdIterations"/>).
    /// </summary>
    public static bool ShouldCompactOnTokenPressure(int estimatedTokens, CompactionConfig config)
    {
        if (!config.IsEnabled) return false;
        if (config.MaxContextTokensTriggerRatio <= 0) return false;
        var tokenTrigger = (int)(config.MaxContextTokens * config.MaxContextTokensTriggerRatio);
        return estimatedTokens >= tokenTrigger;
    }

    /// <summary>
    /// p0362: a fold event — summarize the not-yet-folded middle up to the recent-tail
    /// boundary, advance the watermark, and return the refreshed view. Between fold
    /// events <see cref="BuildView"/> reuses the frozen watermark, so the forwarded
    /// prefix stays byte-stable and the provider prompt cache holds.
    /// </summary>
    private async Task<IEnumerable<ChatMessage>> FoldAsync(
        IList<ChatMessage> messages, CancellationToken ct)
    {
        var headCount = PinnedHeadCount(messages);
        var foldFrom = Math.Max(_foldedUpTo, headCount);
        var tailStart = Math.Max(foldFrom, ComputeTailStart(messages, headCount));
        // Nothing worth summarizing (short middle, or the kept tail already reaches the
        // watermark). Forward the current view — it cannot be reduced further.
        if (tailStart - foldFrom <= 1)
            return BuildView(messages);

        var newMiddle = Slice(messages, foldFrom, tailStart, CurrentPromptIndex(messages, headCount));
        var addition = await SafeSummarizeAsync(newMiddle, ct);
        if (addition is null) return BuildView(messages); // summarizer failed => fail-open
        _summary = string.IsNullOrEmpty(_summary) ? addition : _summary + "\n" + addition;
        _foldedUpTo = tailStart;
        _foldEvents++; _hooks?.OnFold?.Invoke(); // 2026-11-10-85e2

        var result = BuildView(messages);
        _logger?.LogInformation(
            "Compaction fold #{Fold}: {Old} -> {New} messages (watermark {Watermark}, folded {Count} new)",
            _foldEvents, messages.Count, result.Count, _foldedUpTo, newMiddle.Count);
        return result;
    }

    /// <summary>
    /// p0362: the forwarded view. Before the first fold: the raw list. After:
    /// [head | summary | pin | raw[watermark..]] — cache-side-first ordering. The big
    /// append-only summary sits directly behind the byte-stable head so provider prefix
    /// caches cover it; the small pin (ledger + working state, re-rendered CURRENT from
    /// PipelineContext on every call) sits next to the tail, where it also has the
    /// highest model recency. Authority is semantic: the framing text names the ticket
    /// and the pin as overriding the summary.
    /// </summary>
    private IList<ChatMessage> BuildView(IList<ChatMessage> messages)
    {
        if (_foldedUpTo == 0) return messages;

        var headCount = PinnedHeadCount(messages);
        var result = new List<ChatMessage>(headCount + 2 + messages.Count - _foldedUpTo);
        for (var i = 0; i < headCount; i++) result.Add(messages[i]);
        if (CurrentPromptIndex(messages, headCount) is var prompt and >= 0 && prompt < _foldedUpTo)
            result.Add(messages[prompt]);

        if (!string.IsNullOrEmpty(_summary))
            result.Add(new ChatMessage(ChatRole.User,
                "[Context summary of earlier iterations — where it conflicts, the ticket above "
                + "and the current-state block below are authoritative]\n" + _summary));

        var pin = BuildPinText();
        if (!string.IsNullOrWhiteSpace(pin))
            result.Add(new ChatMessage(ChatRole.User,
                "[Current state — authoritative over the summary above]\n" + pin));

        for (var i = _foldedUpTo; i < messages.Count; i++) result.Add(messages[i]);
        return result;
    }

    private string BuildPinText()
    {
        var sb = new StringBuilder();
        var ledger = _hooks?.RenderLedgerForPin?.Invoke();
        if (!string.IsNullOrWhiteSpace(ledger)) sb.AppendLine(ledger).AppendLine();
        var working = _hooks?.RenderWorkingStateForPin?.Invoke();
        if (!string.IsNullOrWhiteSpace(working)) sb.AppendLine(working);
        return sb.ToString().TrimEnd();
    }

    private async Task<string?> SafeSummarizeAsync(IReadOnlyList<ChatMessage> middle, CancellationToken ct)
    {
        try
        {
            return await _summarize(middle, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Context compaction summarizer failed — forwarding the full history");
            return null;
        }
    }

    // p0357: the pinned head is the leading system message(s) PLUS the initial user
    // message. That first user message carries the ticket, the conversation and the
    // attachment sections — the operator-authored instruction manual. It must survive
    // every compaction verbatim; a summarized paraphrase of it is how the master ends
    // up re-deriving APIs the ticket spells out.
    private static int PinnedHeadCount(IList<ChatMessage> messages)
    {
        var n = 0;
        while (n < messages.Count && messages[n].Role == ChatRole.System) n++;
        if (n < messages.Count && messages[n].Role == ChatRole.User) n++;
        return n;
    }

    // Keep the last ~KeepRecentIterations rounds verbatim, but never START the tail on an
    // orphan tool RESULT (its call would have been evicted) — advance past leading Tool
    // messages so the forwarded list stays a valid call/result transcript for every provider.
    private int ComputeTailStart(IList<ChatMessage> messages, int headCount)
    {
        var keep = Math.Max(4, _config.KeepRecentIterations * 2);
        var start = Math.Max(headCount, messages.Count - keep);
        while (start < messages.Count && IsToolResult(messages[start])) start++;
        return start;
    }

    private static bool IsToolResult(ChatMessage m) =>
        m.Role == ChatRole.Tool || m.Contents.OfType<FunctionResultContent>().Any();

    // The pinned current prompt's index past the head, by reference, or -1 when none is pinned or it is gone.
    private int CurrentPromptIndex(IList<ChatMessage> messages, int headCount)
    {
        if (_currentPrompt is null) return -1;
        for (var i = headCount; i < messages.Count; i++)
            if (ReferenceEquals(messages[i], _currentPrompt)) return i;
        return -1;
    }

    private static List<ChatMessage> Slice(IList<ChatMessage> messages, int from, int toExclusive, int skip)
    {
        var slice = new List<ChatMessage>(Math.Max(0, toExclusive - from));
        for (var i = from; i < toExclusive; i++)
            if (i != skip) slice.Add(messages[i]);
        return slice;
    }

    // ~4 chars/token estimate over the message text + tool-call arguments/results.
    internal static int EstimateTokens(IEnumerable<ChatMessage> messages)
    {
        var chars = 0;
        foreach (var m in messages)
            foreach (var c in m.Contents)
                chars += c switch
                {
                    TextContent t => t.Text?.Length ?? 0,
                    FunctionCallContent call => call.Name.Length + (call.Arguments?.Count ?? 0) * 16,
                    FunctionResultContent result => result.Result?.ToString()?.Length ?? 0,
                    _ => 0,
                };
        return chars / CharsPerTokenEstimate;
    }
}
