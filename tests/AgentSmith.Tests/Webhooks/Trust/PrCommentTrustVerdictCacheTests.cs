using AgentSmith.Infrastructure.Services.Webhooks;
using FluentAssertions;

namespace AgentSmith.Tests.Webhooks.Trust;

public sealed class PrCommentTrustVerdictCacheTests
{
    private readonly MovableClock _clock = new(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task GetOrLookupAsync_WithinFiveMinutes_AsksTheHostOnce()
    {
        var sut = new PrCommentTrustVerdictCache(_clock);
        var lookups = 0;

        await sut.GetOrLookupAsync("gitlab:7:42", () => Task.FromResult(++lookups > 0));
        _clock.Advance(TimeSpan.FromMinutes(4));
        var second = await sut.GetOrLookupAsync("gitlab:7:42", () => Task.FromResult(++lookups > 0));

        second.Should().BeTrue();
        lookups.Should().Be(1);
    }

    [Fact]
    public async Task GetOrLookupAsync_AfterFiveMinutes_AsksAgain()
    {
        var sut = new PrCommentTrustVerdictCache(_clock);
        await sut.GetOrLookupAsync("gitlab:7:42", () => Task.FromResult(true));
        _clock.Advance(TimeSpan.FromMinutes(5));

        var revoked = await sut.GetOrLookupAsync("gitlab:7:42", () => Task.FromResult(false));

        revoked.Should().BeFalse();
    }

    [Fact]
    public async Task GetOrLookupAsync_LookupThrows_RemembersNothing()
    {
        var sut = new PrCommentTrustVerdictCache(_clock);

        var failing = () => sut.GetOrLookupAsync("k", () => throw new HttpRequestException("down"));
        await failing.Should().ThrowAsync<HttpRequestException>();

        (await sut.GetOrLookupAsync("k", () => Task.FromResult(true))).Should().BeTrue();
    }

    [Fact]
    public async Task GetOrLookupAsync_KeysAreSeparate()
    {
        var sut = new PrCommentTrustVerdictCache(_clock);
        await sut.GetOrLookupAsync("gitlab:7:42", () => Task.FromResult(true));

        (await sut.GetOrLookupAsync("gitlab:7:43", () => Task.FromResult(false))).Should().BeFalse();
    }

    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
