using System.Text;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Agent.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// p0341d: in-loop context compaction as a provider-agnostic <see cref="DelegatingChatClient"/>
/// in the master chain, below UseFunctionInvocation and below the p0341c governor. The
/// FunctionInvokingChatClient re-sends its growing message list through the inner pipeline
/// every iteration, so this per-call reducer compacts TRANSPARENTLY mid-pass: once the
/// accumulated list crosses the threshold it forwards a reduced view — the system prompt +
/// the current ledger + the p0341c working-state block + the recent tail VERBATIM, plus a
/// cached incremental summary of the evicted middle — instead of the full history. This is
/// the interactive-harness frame: one continuous pass that preserves the THREAD (warm)
/// instead of dying at the raw context window.
///
/// <para>Revives the p0114 compaction LOGIC (threshold + keep-recent + summarize-middle +
/// incremental summary cache) — reusing <see cref="OpenAiContextCompactor.ShouldCompact"/>
/// as the proven trigger predicate — adapted to the Microsoft.Extensions.AI message model,
/// so OpenAI / Claude / Gemini / Ollama all get identical thread-preserving behaviour from
/// the one shared middleware.</para>
/// </summary>
public sealed class CompactingChatClient : DelegatingChatClient
{
    private const int CharsPerTokenEstimate = 4;

    private readonly CompactionConfig _config;
    private readonly MasterLoopHooks _hooks;
    private readonly Func<IReadOnlyList<ChatMessage>, CancellationToken, Task<string>> _summarize;
    private readonly ILogger? _logger;

    // p0341d: the incremental summary + how many non-system messages it already folds. FIC
    // hands us the full append-only history each call, so we only summarize the NEW middle
    // ([_summarizedCount .. tailStart)) and extend — never re-summarize per iteration.
    private string _summary = string.Empty;
    private int _summarizedCount;
    private int _iterations;

    public CompactingChatClient(
        IChatClient inner,
        CompactionConfig config,
        MasterLoopHooks hooks,
        Func<IReadOnlyList<ChatMessage>, CancellationToken, Task<string>> summarize,
        ILogger? logger = null)
        : base(inner)
    {
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
        _iterations++;
        var list = messages as IList<ChatMessage> ?? messages.ToList();

        if (!OpenAiContextCompactor.ShouldCompact(_iterations, EstimateTokens(list), _config))
            return await base.GetResponseAsync(list, options, cancellationToken);

        var compacted = await BuildCompactedAsync(list, cancellationToken);
        return await base.GetResponseAsync(compacted, options, cancellationToken);
    }

    private async Task<IEnumerable<ChatMessage>> BuildCompactedAsync(
        IList<ChatMessage> messages, CancellationToken ct)
    {
        var systemCount = LeadingSystemCount(messages);
        var tailStart = ComputeTailStart(messages, systemCount);
        // Nothing worth summarizing (short prefix, or the tail already covers the middle).
        if (tailStart - systemCount <= 1)
            return messages;

        // Summarize only the newly-evicted middle, then extend the cached summary.
        var newMiddleFrom = systemCount + _summarizedCount;
        if (tailStart > newMiddleFrom)
        {
            var newMiddle = Slice(messages, newMiddleFrom, tailStart);
            var addition = await SafeSummarizeAsync(newMiddle, ct);
            if (addition is null) return messages; // summarizer failed => forward original (fail-open)
            _summary = string.IsNullOrEmpty(_summary) ? addition : _summary + "\n" + addition;
            _summarizedCount = tailStart - systemCount;
        }

        var result = new List<ChatMessage>(messages.Count);
        for (var i = 0; i < systemCount; i++) result.Add(messages[i]);

        // The PIN — rendered CURRENT from PipelineContext at compaction time, never a
        // pass-start snapshot. The summary must never fold these in.
        var pin = BuildPinText();
        if (!string.IsNullOrWhiteSpace(pin))
            result.Add(new ChatMessage(ChatRole.User, pin));
        if (!string.IsNullOrEmpty(_summary))
            result.Add(new ChatMessage(ChatRole.User,
                "[Context summary from earlier iterations — pinned state above is authoritative]\n" + _summary));

        for (var i = tailStart; i < messages.Count; i++) result.Add(messages[i]);

        _logger?.LogInformation(
            "Compacted master context {Old} -> {New} messages (tail kept from {TailStart}, summarized {Folded})",
            messages.Count, result.Count, tailStart, _summarizedCount);
        return result;
    }

    private string BuildPinText()
    {
        var sb = new StringBuilder();
        var ledger = _hooks.RenderLedgerForPin?.Invoke();
        if (!string.IsNullOrWhiteSpace(ledger)) sb.AppendLine(ledger).AppendLine();
        var working = _hooks.RenderWorkingStateForPin?.Invoke();
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

    private static int LeadingSystemCount(IList<ChatMessage> messages)
    {
        var n = 0;
        while (n < messages.Count && messages[n].Role == ChatRole.System) n++;
        return n;
    }

    // Keep the last ~KeepRecentIterations rounds verbatim, but never START the tail on an
    // orphan tool RESULT (its call would have been evicted) — advance past leading Tool
    // messages so the forwarded list stays a valid call/result transcript for every provider.
    private int ComputeTailStart(IList<ChatMessage> messages, int systemCount)
    {
        var keep = Math.Max(4, _config.KeepRecentIterations * 2);
        var start = Math.Max(systemCount, messages.Count - keep);
        while (start < messages.Count && IsToolResult(messages[start])) start++;
        return start;
    }

    private static bool IsToolResult(ChatMessage m) =>
        m.Role == ChatRole.Tool || m.Contents.OfType<FunctionResultContent>().Any();

    private static List<ChatMessage> Slice(IList<ChatMessage> messages, int from, int toExclusive)
    {
        var slice = new List<ChatMessage>(Math.Max(0, toExclusive - from));
        for (var i = from; i < toExclusive; i++) slice.Add(messages[i]);
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
