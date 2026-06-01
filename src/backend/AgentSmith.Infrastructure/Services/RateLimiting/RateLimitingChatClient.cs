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

    private static int EstimateInputTokens(IEnumerable<ChatMessage> messages)
    {
        var chars = 0;
        foreach (var m in messages)
        {
            if (m.Contents is null) continue;
            foreach (var c in m.Contents)
            {
                if (c is TextContent tc && tc.Text is { } t)
                {
                    chars += t.Length;
                }
            }
        }
        return Math.Max(1, chars / CharsPerToken);
    }
}
