using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Bus;
using AgentSmith.Tests.Server;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace AgentSmith.Tests.Infrastructure;

/// <summary>The tail is where a new reader starts; a subscription reads only what follows it.</summary>
public sealed class RedisMessageBusTests
{
    private readonly FakeRedisStreams _redis = new();
    private readonly RedisMessageBus _bus;

    public RedisMessageBusTests() =>
        _bus = new RedisMessageBus(_redis.Connection, NullLogger<RedisMessageBus>.Instance);

    [Fact]
    public async Task TailAsync_NoStream_IsTheStreamStart() =>
        (await _bus.TailAsync("job-x", CancellationToken.None)).Should().Be("0-0");

    [Fact]
    public async Task TailAsync_StreamWithEntries_IsTheNewestId()
    {
        await AddAsync("Progress", "one");
        var newest = await AddAsync("Progress", "two");

        (await _bus.TailAsync("job-1", CancellationToken.None)).Should().Be(newest);
    }

    [Fact]
    public async Task SubscribeToJobAsync_AfterTheTail_ReadsOnlyLaterEntries()
    {
        await AddAsync("Progress", "before");
        var tail = await _bus.TailAsync("job-1", CancellationToken.None);
        await AddAsync("Progress", "after");
        await AddAsync("Done", "finished");

        var texts = new List<string>();
        await foreach (var message in _bus.SubscribeToJobAsync("job-1", tail, CancellationToken.None))
            texts.Add(message.Text);

        texts.Should().Equal("after", string.Empty);
    }

    private async Task<string> AddAsync(string type, string text) =>
        (await _redis.Connection.GetDatabase().StreamAddAsync("job:job-1:out",
            [new NameValueEntry("type", type), new NameValueEntry("text", text)])).ToString();
}
