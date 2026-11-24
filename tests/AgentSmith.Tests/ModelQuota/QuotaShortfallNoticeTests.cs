using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.ModelQuota;

/// <summary>2026-11-24-9dc0d: a shortfall names the output by what the provider counts — Azure's generated tokens, OpenAI's
/// max_tokens.</summary>
public sealed class QuotaShortfallNoticeTests
{
    [Theory]
    [InlineData(true, "input ~299000 + generated 1000")]
    [InlineData(false, "input ~299000 + max_tokens 1000")]
    public async Task UsedTooMuchAsync_NamesTheCountedOutput(bool generated, string expected)
    {
        var notice = new QuotaShortfallNotice("dep", null, NullLogger.Instance);

        await notice.UsedTooMuchAsync(new QuotaShortfall(300_000, 299_000, 1_000, 250_000) { IsGenerated = generated }, CancellationToken.None);

        notice.Reason.Should().Contain(expected);
    }
}
