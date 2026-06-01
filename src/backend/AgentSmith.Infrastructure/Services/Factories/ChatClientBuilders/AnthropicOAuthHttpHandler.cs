namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;

/// <summary>
/// p0187: rewrites the per-request auth header when the configured Anthropic
/// token is a subscription / OAuth token (prefix <c>sk-ant-oat</c>). The
/// vendored Anthropic.SDK 5.10.0 only knows the API-key flow and unconditionally
/// sends an <c>x-api-key</c> header; the Anthropic API rejects an OAuth token
/// sent that way with <c>invalid x-api-key</c>. Authorization Bearer is the
/// documented alternative for the same endpoint.
///
/// Also handles 429 / 529 rate-limit responses (which the subscription tier
/// hits when the analyzer fans out across 5+ sandboxes) by sleeping for the
/// server-provided Retry-After interval and retrying up to MaxRetries times
/// with exponential fallback when the header is absent.
///
/// The header rewrite is a no-op for tokens that don't start with <c>sk-ant-oat</c>
/// (regular API keys still ride <c>x-api-key</c>); the retry-on-429 logic
/// applies to both paths because rate-limit pressure is independent of auth flow.
/// </summary>
internal sealed class AnthropicOAuthHttpHandler : DelegatingHandler
{
    private const string OAuthPrefix = "sk-ant-oat";
    private const int MaxRetries = 5;
    private static readonly TimeSpan DefaultBackoff = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    private readonly string _token;

    public AnthropicOAuthHttpHandler(string token, HttpMessageHandler inner)
    {
        _token = token;
        InnerHandler = inner;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ApplyAuthHeaders(request);
        var attempt = 0;
        while (true)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode is not (System.Net.HttpStatusCode.TooManyRequests or (System.Net.HttpStatusCode)529)
                || attempt >= MaxRetries)
            {
                return response;
            }
            var delay = ResolveRetryDelay(response, attempt);
            response.Dispose();
            await Task.Delay(delay, cancellationToken);
            attempt++;
            // Reapply auth headers on the request — Authorization may have been
            // consumed by the previous send. Idempotent in practice.
            ApplyAuthHeaders(request);
        }
    }

    private void ApplyAuthHeaders(HttpRequestMessage request)
    {
        if (!_token.StartsWith(OAuthPrefix, StringComparison.Ordinal)) return;
        request.Headers.Remove("x-api-key");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        // Claude's OAuth flow requires the anthropic-beta + anthropic-version
        // headers that Claude Code sends; the SDK already sets anthropic-version
        // so only the beta flag is added defensively for endpoints that gate
        // OAuth behind it.
        if (!request.Headers.Contains("anthropic-beta"))
        {
            request.Headers.Add("anthropic-beta", "oauth-2025-04-20");
        }
    }

    private static TimeSpan ResolveRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter is { } retryAfter)
        {
            if (retryAfter.Delta is { } delta && delta > TimeSpan.Zero)
                return Clamp(delta);
            if (retryAfter.Date is { } when)
            {
                var diff = when - DateTimeOffset.UtcNow;
                if (diff > TimeSpan.Zero) return Clamp(diff);
            }
        }
        // Exponential fallback: 5s, 10s, 20s, 40s, 60s (capped at MaxBackoff).
        var exp = TimeSpan.FromTicks(DefaultBackoff.Ticks * (long)Math.Pow(2, attempt));
        return Clamp(exp);
    }

    private static TimeSpan Clamp(TimeSpan delay) => delay > MaxBackoff ? MaxBackoff : delay;
}
