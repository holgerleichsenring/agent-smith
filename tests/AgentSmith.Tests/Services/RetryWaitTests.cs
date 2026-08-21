using System.ClientModel;
using System.ClientModel.Primitives;
using System.Globalization;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.RateLimiting;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0493: a rate limit is a different waiting class from a socket fault. A run on image
/// 0.134.0 died with "HTTP 429 (too_many_requests: rate_limit_exceeded) Your requests to
/// … in swedencentral have exceeded rate limit" — on a ladder (4s, 8s, 16s, 32s, 60s) sized
/// for a connection that dropped mid-body, while an Azure throughput window is sixty seconds
/// wide and the server says so in a header nobody on this path read.
/// </summary>
public sealed class RetryWaitTests
{
    /// <summary>The operator's live configuration.</summary>
    private static RetryConfig Operator() =>
        new() { MaxRetries = 5, InitialDelayMs = 4000, BackoffMultiplier = 2, MaxDelayMs = 60000 };

    private static Exception RateLimited(params (string Name, string Value)[] headers) =>
        new ClientResultException("rate_limit_exceeded", new FakeResponse(429, headers));

    [Fact]
    public void RetryWait_ARateLimitCarryingRetryAfter_WaitsTheServersOwnInterval()
    {
        var wait = RetryWait.For(Operator(), 0, RateLimited(("Retry-After", "47")));

        wait.Delay.Should().Be(TimeSpan.FromSeconds(47),
            "the server named the interval, so guessing one is strictly worse");
        wait.Reason.Should().Contain("asked for");
    }

    [Fact]
    public void RetryWait_ARetryAfterAboveTheCeiling_IsCappedAndSaysSo()
    {
        var wait = RetryWait.For(Operator(), 0, RateLimited(("Retry-After", "3600")));

        wait.Delay.Should().Be(RetryWait.Ceiling,
            "an hour is a quota no run can wait out, and parking on it is a silent hang");
        wait.Reason.Should().Contain("3600s").And.Contain("capped",
            "a clamp the log does not mention is a clamp nobody can diagnose");
    }

    [Fact]
    public void RetryWait_ARetryAfterDate_IsReadAsAnInterval()
    {
        var when = DateTimeOffset.UtcNow.AddSeconds(30).ToString("R", CultureInfo.InvariantCulture);

        var wait = RetryWait.For(Operator(), 0, RateLimited(("Retry-After", when)));

        wait.Delay.Should().BeCloseTo(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(3),
            "the header is allowed to name an absolute moment instead of a duration");
    }

    /// <summary>OpenAI answers in milliseconds under its own header name.</summary>
    [Fact]
    public void RetryWait_ARetryAfterInMilliseconds_IsRead()
    {
        var wait = RetryWait.For(Operator(), 0, RateLimited(("retry-after-ms", "1500")));

        wait.Delay.Should().Be(TimeSpan.FromMilliseconds(1500));
    }

    [Fact]
    public void RetryWait_ARateLimitWithNoRetryAfter_WaitsTheRateLimitLadderNotTheNetworkOne()
    {
        var wait = RetryWait.For(Operator(), 0, RateLimited());

        wait.Delay.Should().Be(RetryWait.RateLimitFloor,
            "four seconds against a sixty-second refill window learns nothing and spends an attempt");
        wait.Delay.Should().BeGreaterThan(TimeSpan.FromMilliseconds(Operator().InitialDelayMs));
        wait.Reason.Should().Contain("no Retry-After");
    }

    [Fact]
    public void RetryWait_ANetworkFault_KeepsTheConfiguredLadder()
    {
        var retry = Operator();

        RetryWait.For(retry, 0, new IOException("The response ended prematurely."))
            .Delay.Should().Be(TimeSpan.FromMilliseconds(4000), "p0374's ladder is unchanged for a socket fault");
        RetryWait.For(retry, 2, new IOException("drop"))
            .Delay.Should().Be(TimeSpan.FromMilliseconds(16000));
        RetryWait.For(retry, 0, new IOException("drop"))
            .Reason.Should().Contain("network");
    }

    [Fact]
    public void RetryWait_ARateLimitLadder_NeverExceedsTheCeiling()
    {
        var retry = Operator();

        foreach (var attempt in Enumerable.Range(0, retry.MaxRetries))
        {
            RetryWait.For(retry, attempt, RateLimited()).Delay
                .Should().BeLessThanOrEqualTo(RetryWait.Ceiling, "no single wait may exceed the ceiling");
        }
    }

    [Fact]
    public void RetryWait_ARateLimit_IsNotCalledANetworkError()
    {
        RetryWait.For(Operator(), 0, RateLimited()).Reason
            .Should().Contain("rate limit").And.NotContain("network",
                "since p0477 the warning said 'network error' for a 429 and sent the reader hunting");
        RetryWait.IsRateLimit(RateLimited()).Should().BeTrue();
        RetryWait.IsRateLimit(new ClientResultException("server", new FakeResponse(503))).Should().BeFalse(
            "a 5xx is the server's own fault, not a throughput refusal");
    }

    /// <summary>p0376's other SDK family reports a status as an HttpRequestException, which
    /// carries no response — the class is still recognised, the header simply is not there.
    /// </summary>
    [Fact]
    public void RetryWait_ARateLimitWithNoResponseToRead_StillWaitsTheRateLimitLadder()
    {
        var ex = new HttpRequestException("too many", null, System.Net.HttpStatusCode.TooManyRequests);

        RetryWait.IsRateLimit(ex).Should().BeTrue();
        RetryWait.RetryAfter(ex).Should().BeNull();
        RetryWait.For(Operator(), 0, ex).Delay.Should().Be(RetryWait.RateLimitFloor);
    }
}
