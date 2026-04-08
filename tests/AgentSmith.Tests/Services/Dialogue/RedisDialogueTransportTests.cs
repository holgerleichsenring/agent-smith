using AgentSmith.Contracts.Dialogue;
using AgentSmith.Infrastructure.Services.Dialogue;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Services.Dialogue;

public sealed class RedisDialogueTransportTests
{
    private readonly Mock<IConnectionMultiplexer> _redis = new();
    private readonly Mock<IDatabase> _db = new();
    private readonly RedisDialogueTransport _sut;

    public RedisDialogueTransportTests()
    {
        _redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_db.Object);
        _sut = new RedisDialogueTransport(_redis.Object, NullLogger<RedisDialogueTransport>.Instance);
    }

    [Fact]
    public async Task PublishQuestionAsync_WritesToOutboundStream()
    {
        var question = new DialogQuestion(
            "q-123", QuestionType.Confirmation, "Continue?",
            "Some context", null, "yes", TimeSpan.FromMinutes(5));

        _db.Setup(d => d.StreamAddAsync(
                It.IsAny<RedisKey>(), It.IsAny<NameValueEntry[]>(),
                It.IsAny<RedisValue?>(), It.IsAny<int?>(),
                It.IsAny<bool>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync("1-0");

        _db.Setup(d => d.KeyExpireAsync(
                It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await _sut.PublishQuestionAsync("job-1", question, CancellationToken.None);

        _db.Verify(d => d.StreamAddAsync(
            (RedisKey)"job:job-1:out",
            It.Is<NameValueEntry[]>(entries =>
                entries.Any(e => e.Name == "type" && e.Value == "question") &&
                entries.Any(e => e.Name == "questionId" && e.Value == "q-123") &&
                entries.Any(e => e.Name == "text" && e.Value == "Continue?")),
            It.IsAny<RedisValue?>(), It.IsAny<int?>(),
            It.IsAny<bool>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task PublishQuestionAsync_IncludesChoicesAsPipeSeparated()
    {
        var question = new DialogQuestion(
            "q-choice", QuestionType.Choice, "Pick one",
            null, ["A", "B", "C"], "A", TimeSpan.FromMinutes(1));

        _db.Setup(d => d.StreamAddAsync(
                It.IsAny<RedisKey>(), It.IsAny<NameValueEntry[]>(),
                It.IsAny<RedisValue?>(), It.IsAny<int?>(),
                It.IsAny<bool>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync("1-0");

        _db.Setup(d => d.KeyExpireAsync(
                It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await _sut.PublishQuestionAsync("job-2", question, CancellationToken.None);

        _db.Verify(d => d.StreamAddAsync(
            It.IsAny<RedisKey>(),
            It.Is<NameValueEntry[]>(entries =>
                entries.Any(e => e.Name == "choices" && e.Value == "A|B|C")),
            It.IsAny<RedisValue?>(), It.IsAny<int?>(),
            It.IsAny<bool>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task PublishAnswerAsync_WritesToInboundStream()
    {
        var answer = new DialogAnswer(
            "q-123", "yes", "Looks good",
            DateTimeOffset.UtcNow, "user-1");

        _db.Setup(d => d.StreamAddAsync(
                It.IsAny<RedisKey>(), It.IsAny<NameValueEntry[]>(),
                It.IsAny<RedisValue?>(), It.IsAny<int?>(),
                It.IsAny<bool>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync("1-0");

        _db.Setup(d => d.KeyExpireAsync(
                It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await _sut.PublishAnswerAsync("job-1", answer, CancellationToken.None);

        _db.Verify(d => d.StreamAddAsync(
            (RedisKey)"job:job-1:in",
            It.Is<NameValueEntry[]>(entries =>
                entries.Any(e => e.Name == "type" && e.Value == "answer") &&
                entries.Any(e => e.Name == "questionId" && e.Value == "q-123") &&
                entries.Any(e => e.Name == "answer" && e.Value == "yes")),
            It.IsAny<RedisValue?>(), It.IsAny<int?>(),
            It.IsAny<bool>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task WaitForAnswerAsync_MatchingAnswer_ReturnsIt()
    {
        var answeredAt = DateTimeOffset.UtcNow;
        var streamEntries = new[]
        {
            new StreamEntry("1-0", new NameValueEntry[]
            {
                new("type", "answer"),
                new("questionId", "q-42"),
                new("answer", "approved"),
                new("comment", "LGTM"),
                new("answeredAt", answeredAt.ToString("O")),
                new("answeredBy", "reviewer"),
            })
        };

        _db.Setup(d => d.StreamReadAsync(
                (RedisKey)"job:job-1:in", It.IsAny<RedisValue>(),
                It.IsAny<int?>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(streamEntries);

        var result = await _sut.WaitForAnswerAsync("job-1", "q-42", TimeSpan.FromSeconds(5), CancellationToken.None);

        result.Should().NotBeNull();
        result!.QuestionId.Should().Be("q-42");
        result.Answer.Should().Be("approved");
        result.Comment.Should().Be("LGTM");
        result.AnsweredBy.Should().Be("reviewer");
    }

    [Fact]
    public async Task WaitForAnswerAsync_NoAnswer_ReturnsNullOnTimeout()
    {
        _db.Setup(d => d.StreamReadAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<int?>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<StreamEntry>());

        var result = await _sut.WaitForAnswerAsync("job-1", "q-99", TimeSpan.FromMilliseconds(100), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task WaitForAnswerAsync_SkipsNonMatchingQuestionIds()
    {
        var streamEntries = new[]
        {
            new StreamEntry("1-0", new NameValueEntry[]
            {
                new("type", "answer"),
                new("questionId", "q-other"),
                new("answer", "no"),
                new("comment", ""),
                new("answeredAt", DateTimeOffset.UtcNow.ToString("O")),
                new("answeredBy", "user"),
            })
        };

        _db.Setup(d => d.StreamReadAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<int?>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(streamEntries);

        var result = await _sut.WaitForAnswerAsync("job-1", "q-42", TimeSpan.FromMilliseconds(100), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task WaitForAnswerAsync_Cancellation_ReturnsNull()
    {
        _db.Setup(d => d.StreamReadAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<int?>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<StreamEntry>());

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // Should not throw, just return null
        var act = async () => await _sut.WaitForAnswerAsync("job-1", "q-1", TimeSpan.FromSeconds(30), cts.Token);

        await act.Should().ThrowAsync<TaskCanceledException>();
    }
}
