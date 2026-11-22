using System.Net;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.RateLimiting;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using FluentAssertions;

namespace AgentSmith.Tests.ModelQuota;

/// <summary>
/// 2026-11-22-66e4a: the Anthropic SDK reads the server's interval into RateLimitsExceeded, which p0493 thought
/// carried nothing; it is a throughput refusal and waits the interval the server named, not the 15 s floor.
/// </summary>
public sealed class ModelQuotaAnthropicRefusalTests
{
    [Fact]
    public void RetryWait_AnthropicRateLimitsExceeded_WaitsTheServersInterval()
    {
        var refusal = new RateLimitsExceeded(
            "rate_limit_error", new RateLimits { RetryAfter = TimeSpan.FromSeconds(3) }, HttpStatusCode.TooManyRequests);

        new ProviderRefusalReader(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance).IsThroughputRefusal(refusal).Should().BeTrue();
        RetryWait.For(new RetryConfig(), 0, refusal).Delay.Should().Be(TimeSpan.FromSeconds(3));
    }
}
