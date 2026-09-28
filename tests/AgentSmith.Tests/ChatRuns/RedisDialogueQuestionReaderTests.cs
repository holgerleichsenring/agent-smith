using AgentSmith.Contracts.Dialogue;
using AgentSmith.Infrastructure.Services.Dialogue;
using AgentSmith.Tests.Server;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace AgentSmith.Tests.ChatRuns;

/// <summary>The reader reads back what the dialogue transport writes, from the same stream.</summary>
public sealed class RedisDialogueQuestionReaderTests
{
    private readonly FakeRedisStreams _redis = new();

    [Fact]
    public async Task Latest_ReadsTheNewestQuestionTheTransportPublished()
    {
        var transport = new RedisDialogueTransport(_redis.Connection, NullLogger<RedisDialogueTransport>.Instance);
        await transport.PublishQuestionAsync("run-1", Question("q1", QuestionType.Confirmation, null), default);
        await transport.PublishQuestionAsync("run-1",
            Question("q2", QuestionType.Choice, [new DialogChoice("left"), new DialogChoice("right")]), default);
        await _redis.Connection.GetDatabase().StreamAddAsync(
            "job:run-1:out", [new NameValueEntry("type", "Progress"), new NameValueEntry("text", "step 3")]);

        var latest = await new RedisDialogueQuestionReader(_redis.Connection).LatestAsync("run-1", default);

        latest!.QuestionId.Should().Be("q2", "a progress line after it is not a question");
        latest.Type.Should().Be(QuestionType.Choice);
        latest.Choices!.Select(c => c.Label).Should().Equal("left", "right");
        latest.Timeout.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task Latest_NoStream_IsNull() =>
        (await new RedisDialogueQuestionReader(_redis.Connection).LatestAsync("run-x", default))
            .Should().BeNull();

    private static DialogQuestion Question(string id, QuestionType type, IReadOnlyList<DialogChoice>? choices) =>
        new(id, type, "Which way?", "context", choices, null, TimeSpan.FromMinutes(30));
}
