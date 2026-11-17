using System.Text;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// In-loop context compaction below UseFunctionInvocation, provider-agnostic. The function-invoking client re-sends
/// its growing list every iteration; once the forwarded VIEW crosses MaxContextTokens × ratio, the middle is folded
/// into a summary segment and the watermark frozen, so between folds the view [head | segments | pin | tail]
/// grows append-only with a byte-stable prefix the provider cache holds. The pinned head is the system message(s)
/// plus the first user message, kept verbatim.
/// <para>
/// 2026-11-17-cb32a: a fold shrinks the view below <see cref="CompactionConfig.FoldTargetRatio"/>, so many calls
/// pass before the next one; it appends its summary as a new segment, leaving the earlier ones as they were, and
/// renders the pin once — the state at that fold — kept until the next fold.
/// </para>
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

    // p0341d/p0362: the summary segments + the absolute raw index up to which history is folded into them. FIC hands
    // us the full append-only history each call; we summarize only [_foldedUpTo .. tailStart) at fold events.
    // _foldedUpTo == 0 means no fold event has happened yet. 2026-11-17-cb32a: one segment per fold, and the pin
    // rendered at that fold.
    private readonly List<string> _segments = [];
    private string _pin = string.Empty;
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
    /// p0362: a fold event — summarize the not-yet-folded middle up to the tail, advance the watermark, and return
    /// the refreshed view. 2026-11-17-cb32a: the tail is sized so the view lands under the fold target; the summary
    /// becomes a new segment, or — when the segments would pass their budget — one segment for all of them.
    /// </summary>
    private async Task<IEnumerable<ChatMessage>> FoldAsync(
        IList<ChatMessage> messages, CancellationToken ct)
    {
        var headCount = CompactionTail.PinnedHeadCount(messages);
        var prompt = CurrentPromptIndex(messages, headCount);
        var foldFrom = Math.Max(_foldedUpTo, headCount);
        var pin = BuildPinText();
        var consolidates = Consolidates();
        var fixedTokens = EstimateTokens(messages.Take(headCount)) + (prompt >= 0 ? EstimateTokens([messages[prompt]]) : 0)
            + (consolidates ? 0 : SegmentTokens()) + pin.Length / CharsPerTokenEstimate;
        var budget = (int)(_config.MaxContextTokens * _config.FoldTargetRatio) - fixedTokens;
        var tailStart = Math.Max(foldFrom,
            CompactionTail.Start(messages, headCount, Math.Max(4, _config.KeepRecentIterations * 2), budget, prompt));
        // Nothing worth summarizing (short middle, or the kept tail already reaches the
        // watermark). Forward the current view — it cannot be reduced further.
        if (tailStart - foldFrom <= 1)
            return BuildView(messages);

        var newMiddle = Slice(messages, foldFrom, tailStart, prompt);
        if (consolidates)
            newMiddle.InsertRange(0, _segments.Select(s => new ChatMessage(ChatRole.User, "[Earlier summary]\n" + s)));
        var addition = await SafeSummarizeAsync(newMiddle, ct);
        if (addition is null) return BuildView(messages); // summarizer failed => fail-open
        if (consolidates) _segments.Clear();
        _segments.Add(addition);
        _pin = pin;
        _foldedUpTo = tailStart;
        _foldEvents++;

        var result = BuildView(messages);
        _logger?.LogInformation(
            "Compaction fold #{Fold}: {Old} -> {New} messages (watermark {Watermark}, folded {Count} new, {Segments} summary segment(s){Consolidated})",
            _foldEvents, messages.Count, result.Count, _foldedUpTo, newMiddle.Count, _segments.Count,
            consolidates ? ", earlier segments consolidated" : "");
        return result;
    }

    // The segments would pass their budget with one more of their average size.
    private bool Consolidates()
    {
        if (_segments.Count == 0) return false;
        var tokens = SegmentTokens();
        return tokens + tokens / _segments.Count > _config.MaxContextTokens * _config.SummaryBudgetRatio;
    }

    private int SegmentTokens() => _segments.Sum(s => s.Length) / CharsPerTokenEstimate;

    /// <summary>
    /// p0362: the forwarded view. Before the first fold: the raw list. After:
    /// [head | current prompt | segments | pin | raw[watermark..]] — cache-side-first ordering. The segments sit
    /// directly behind the byte-stable head, each left as its fold wrote it; the pin, rendered at the last fold, sits
    /// next to the tail. Authority is semantic: the framing text names the ticket and the pin as overriding the
    /// summary, and the messages after the pin as newer than it.
    /// </summary>
    private IList<ChatMessage> BuildView(IList<ChatMessage> messages)
    {
        if (_foldedUpTo == 0) return messages;

        var headCount = CompactionTail.PinnedHeadCount(messages);
        var result = new List<ChatMessage>(headCount + _segments.Count + 2 + messages.Count - _foldedUpTo);
        for (var i = 0; i < headCount; i++) result.Add(messages[i]);
        if (CurrentPromptIndex(messages, headCount) is var prompt and >= 0 && prompt < _foldedUpTo)
            result.Add(messages[prompt]);

        for (var k = 0; k < _segments.Count; k++)
            result.Add(new ChatMessage(ChatRole.User,
                $"[Context summary of earlier iterations, part {k + 1} — where it conflicts, the ticket above "
                + "and the state block below are authoritative]\n" + _segments[k]));

        if (!string.IsNullOrWhiteSpace(_pin))
            result.Add(new ChatMessage(ChatRole.User,
                "[State at the latest summary — authoritative over the summary above; the messages below are newer]\n" + _pin));

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
