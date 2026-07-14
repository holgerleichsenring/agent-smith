using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// p0188: <see cref="IChatClient"/> decorator that blocks the call until the
/// shared <see cref="ILlmRateLimiter"/> has capacity for one request and the
/// estimated input-token budget. Input tokens are estimated from the message
/// characters at chars/4 (rough Latin-script approximation; conservative
/// enough for both Anthropic and OpenAI tokenisation).
/// </summary>
internal sealed class RateLimitingChatClient : DelegatingChatClient
{
    private const int CharsPerToken = 4;

    private readonly ILlmRateLimiter _limiter;
    private readonly string _label;
    private readonly ILogger<RateLimitingChatClient> _logger;

    public RateLimitingChatClient(
        IChatClient inner,
        ILlmRateLimiter limiter,
        string label,
        ILogger<RateLimitingChatClient> logger) : base(inner)
    {
        _limiter = limiter;
        _label = label;
        _logger = logger;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var materialised = messages as IReadOnlyCollection<ChatMessage> ?? messages.ToList();
        var estimated = EstimateInputTokens(materialised);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var lease = await _limiter.AcquireAsync(estimated, cancellationToken);
        if (sw.ElapsedMilliseconds > 500)
        {
            _logger.LogInformation(
                "Rate-limited {Label}: waited {WaitMs}ms before sending (~{Tokens} tokens estimated)",
                _label, sw.ElapsedMilliseconds, estimated);
        }
        return await base.GetResponseAsync(materialised, options, cancellationToken);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var materialised = messages as IReadOnlyCollection<ChatMessage> ?? messages.ToList();
        var estimated = EstimateInputTokens(materialised);
        using var lease = await _limiter.AcquireAsync(estimated, cancellationToken);
        await foreach (var update in base.GetStreamingResponseAsync(materialised, options, cancellationToken))
        {
            yield return update;
        }
    }

    // Internal for direct unit testing of the estimate.
    internal static int EstimateInputTokens(IEnumerable<ChatMessage> messages)
    {
        var chars = 0;
        foreach (var m in messages)
        {
            if (m.Contents is null) continue;
            foreach (var c in m.Contents)
                chars += EstimateContentChars(c);
        }
        return Math.Max(1, chars / CharsPerToken);
    }

    // An agentic loop re-sends the WHOLE conversation each turn, and the bulk of
    // it is tool traffic — the model's function calls and, above all, the tool
    // RESULTS (grep output, file reads). Counting only TextContent undercounted
    // the input ~7x on a large coding run (25k estimated vs ~190k actual), so the
    // limiter throttled on a fiction while the real per-minute load blew past the
    // provider quota. Count every content kind by its rendered length.
    private static int EstimateContentChars(AIContent content) => content switch
    {
        TextContent t => t.Text?.Length ?? 0,
        FunctionCallContent call => (call.Name?.Length ?? 0) + ArgumentsLength(call.Arguments),
        FunctionResultContent result => result.Result?.ToString()?.Length ?? 0,
        _ => 0,
    };

    private static int ArgumentsLength(IDictionary<string, object?>? arguments)
    {
        if (arguments is null) return 0;
        var chars = 0;
        foreach (var (key, value) in arguments)
            chars += key.Length + (value?.ToString()?.Length ?? 0);
        return chars;
    }
}
