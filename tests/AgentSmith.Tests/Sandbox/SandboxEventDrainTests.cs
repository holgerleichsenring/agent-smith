using System.Text.Json;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0491: the channel used to read ONE 100-entry page per poll tick and return the instant
/// the result popped, so a command that printed more lines than that had the rest left in the
/// stream — where the next step read them, saw another step's id, and dropped them. The
/// sandbox then reported empty output for every short command until a long one gave the
/// reader time to catch up, which is why the loss moved between repositories.
/// </summary>
public sealed class SandboxEventDrainTests
{
    private static readonly Guid StepId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public async Task WaitForResultAsync_BurstLargerThanOneReadWindow_ForwardsEveryEvent()
    {
        var stream = new FakeStream(lines: 250);
        var seen = new List<StepEvent>();

        var result = await Channel(stream).WaitForResultAsync(
            StepId, new Progress(seen), TimeSpan.FromSeconds(5), CancellationToken.None);

        result.StepId.Should().Be(StepId);
        seen.Should().HaveCount(250, "every line the command printed belongs to its step");
    }

    [Fact]
    public async Task WaitForResultAsync_ResultAlreadyWaiting_StillForwardsTheEventsBehindIt()
    {
        // The agent's last lines land after the drain that preceded the pop.
        var stream = new FakeStream(lines: 0);
        var seen = new List<StepEvent>();
        stream.OnFirstRead = () => stream.Append(40);

        await Channel(stream).WaitForResultAsync(
            StepId, new Progress(seen), TimeSpan.FromSeconds(5), CancellationToken.None);

        seen.Should().HaveCount(40, "the drain after the pop is what catches them");
    }

    [Fact]
    public async Task WaitForResultAsync_StreamThatKeepsGrowing_StopsAtTheBoundInsteadOfSpinning()
    {
        // A producer that never quiesces: every read returns a full page.
        var stream = new FakeStream(lines: 0) { AlwaysFull = true };

        var act = async () => await Channel(stream, withResult: false).WaitForResultAsync(
            StepId, new Progress([]), TimeSpan.FromMilliseconds(1), CancellationToken.None);

        await act.Should().ThrowAsync<TimeoutException>();
        stream.Reads.Should().Be(StreamLimits.EventStreamMaxLength / 100,
            "the catch-up stops at the stream's own cap rather than reading forever");
    }

    private static SandboxRedisChannel Channel(FakeStream stream, bool withResult = true)
    {
        var db = new Mock<IDatabase>();
        db.Setup(d => d.StreamReadAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<int?>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey _, RedisValue position, int? count, CommandFlags __) =>
                stream.Read(position, count ?? 100));
        db.Setup(d => d.ListRightPopAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(() => withResult ? Result() : RedisValue.Null);
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);
        return new SandboxRedisChannel(
            multiplexer.Object, "job-p0491", NullLogger<SandboxRedisChannel>.Instance);
    }

    private static RedisValue Result() => JsonSerializer.Serialize(
        new StepResult(StepResult.CurrentSchemaVersion, StepId, 0, false, 0.26, null, "the whole listing"),
        WireFormat.Json);

    private sealed class Progress(List<StepEvent> seen) : IProgress<StepEvent>
    {
        public void Report(StepEvent value) => seen.Add(value);
    }

    private sealed class FakeStream(int lines)
    {
        private readonly List<StreamEntry> _entries = [];
        public int Reads { get; private set; }
        public bool AlwaysFull { get; init; }
        public Action? OnFirstRead { get; set; }

        public void Append(int count)
        {
            for (var i = 0; i < count; i++)
                _entries.Add(new StreamEntry($"{_entries.Count + 1}-0",
                    [new NameValueEntry("data", Serialize(_entries.Count))]));
        }

        public StreamEntry[] Read(RedisValue position, int count)
        {
            if (Reads++ == 0) Append(lines);
            if (AlwaysFull) return Page(count, Reads * count);
            var after = long.Parse(((string)position!).Split('-')[0]);
            var page = _entries.Skip((int)after).Take(count).ToArray();
            // Whatever the agent writes AFTER this read must still reach the step.
            if (Reads == 1) OnFirstRead?.Invoke();
            return page;
        }

        private static StreamEntry[] Page(int count, int offset) =>
            [.. Enumerable.Range(offset, count).Select(i =>
                new StreamEntry($"{i + 1}-0", [new NameValueEntry("data", Serialize(i))]))];

        private static string Serialize(int index) => JsonSerializer.Serialize(
            new StepEvent(StepEvent.CurrentSchemaVersion, StepId, StepEventKind.Stdout,
                $"line {index}", DateTimeOffset.UtcNow), WireFormat.Json);
    }
}
