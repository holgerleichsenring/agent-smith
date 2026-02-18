using System.Net;
using AgentSmith.Contracts.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace AgentSmith.Infrastructure.Providers.Agent;

/// <summary>
/// Creates HttpClient instances configured with Polly retry policy
/// for handling transient failures and rate limits from the Anthropic API.
/// </summary>
public sealed class ResilientHttpClientFactory(
    RetryConfig config,
    ILogger logger)
{
    private static readonly HashSet<HttpStatusCode> RetryableStatusCodes = new()
    {
        HttpStatusCode.TooManyRequests,       // 429
        HttpStatusCode.InternalServerError,   // 500
        HttpStatusCode.BadGateway,            // 502
        HttpStatusCode.ServiceUnavailable,    // 503
        HttpStatusCode.GatewayTimeout         // 504
    };

    public HttpClient Create()
    {
        var pipeline = CreateResiliencePipeline();
        var handler = new ResilienceHandler(pipeline)
        {
            InnerHandler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            }
        };
        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
    }

    private ResiliencePipeline<HttpResponseMessage> CreateResiliencePipeline()
    {
        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r => RetryableStatusCodes.Contains(r.StatusCode))
                    .Handle<HttpRequestException>(),
                MaxRetryAttempts = config.MaxRetries,
                DelayGenerator = args => ValueTask.FromResult(CalculateDelay(args.AttemptNumber)),
                OnRetry = args =>
                {
                    var statusCode = args.Outcome.Result?.StatusCode;
                    logger.LogWarning(
                        "Retry {Attempt}/{Max} after {Delay}ms (HTTP {Status})",
                        args.AttemptNumber + 1,
                        config.MaxRetries,
                        args.RetryDelay.TotalMilliseconds,
                        statusCode);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    private TimeSpan? CalculateDelay(int attemptNumber)
    {
        var baseDelay = config.InitialDelayMs * Math.Pow(config.BackoffMultiplier, attemptNumber);
        var clampedDelay = Math.Min(baseDelay, config.MaxDelayMs);

        // Add jitter: ±25%
        var jitter = clampedDelay * 0.25 * (Random.Shared.NextDouble() * 2 - 1);
        var finalDelay = TimeSpan.FromMilliseconds(clampedDelay + jitter);

        return finalDelay;
    }
}

/// <summary>
/// DelegatingHandler that uses a Polly ResiliencePipeline for retry logic.
/// </summary>
internal sealed class ResilienceHandler(
    ResiliencePipeline<HttpResponseMessage> pipeline) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await pipeline.ExecuteAsync(
            async ct => await base.SendAsync(request, ct),
            cancellationToken);
    }
}
