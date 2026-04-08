using AgentSmith.Contracts.Dialogue;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Infrastructure.Services.Dialogue;

/// <summary>
/// Redis Streams implementation of <see cref="IDialogueTransport"/>.
/// Questions go on job:{id}:out (agent to dispatcher),
/// answers come on job:{id}:in (dispatcher to agent).
/// Reuses the same stream keys as <see cref="Bus.RedisMessageBus"/>.
/// </summary>
public sealed class RedisDialogueTransport(
    IConnectionMultiplexer redis,
    ILogger<RedisDialogueTransport> logger) : IDialogueTransport
{
    private const int StreamMaxLen = 1000;
    private static readonly TimeSpan KeyTtl = TimeSpan.FromHours(2);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    public async Task PublishQuestionAsync(string jobId, DialogQuestion question, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var streamKey = OutboundKey(jobId);

        var entries = new NameValueEntry[]
        {
            new("type", "question"),
            new("questionId", question.QuestionId),
            new("questionType", question.Type.ToString()),
            new("text", question.Text),
            new("context", question.Context ?? ""),
            new("choices", question.Choices is not null ? string.Join("|", question.Choices) : ""),
            new("defaultAnswer", question.DefaultAnswer ?? ""),
            new("timeoutSeconds", question.Timeout.TotalSeconds.ToString("F0")),
        };

        await db.StreamAddAsync(streamKey, entries, maxLength: StreamMaxLen, useApproximateMaxLength: true);
        await db.KeyExpireAsync(streamKey, KeyTtl);

        logger.LogDebug("Published question {QuestionId} to {Stream}", question.QuestionId, (string?)streamKey);
    }

    public async Task<DialogAnswer?> WaitForAnswerAsync(string jobId, string questionId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var streamKey = InboundKey(jobId);
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        var lastId = "0-0";

        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            var entries = await db.StreamReadAsync(streamKey, lastId, count: 10);

            if (entries is { Length: > 0 })
            {
                foreach (var entry in entries)
                {
                    lastId = entry.Id!;
                    var dict = entry.Values.ToDictionary(e => (string)e.Name!, e => (string?)e.Value);

                    if (dict.GetValueOrDefault("type") != "answer")
                        continue;

                    var entryQuestionId = dict.GetValueOrDefault("questionId") ?? "";
                    if (entryQuestionId != questionId)
                        continue;

                    var answer = new DialogAnswer(
                        entryQuestionId,
                        dict.GetValueOrDefault("answer") ?? "",
                        NullIfEmpty(dict.GetValueOrDefault("comment")),
                        DateTimeOffset.TryParse(dict.GetValueOrDefault("answeredAt"), out var at) ? at : DateTimeOffset.UtcNow,
                        dict.GetValueOrDefault("answeredBy") ?? "unknown");

                    logger.LogDebug("Received answer for question {QuestionId} from {Stream}", questionId, (string?)streamKey);
                    return answer;
                }
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        logger.LogWarning("WaitForAnswerAsync timed out for question {QuestionId} on job {JobId}", questionId, jobId);
        return null;
    }

    public async Task PublishAnswerAsync(string jobId, DialogAnswer answer, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var streamKey = InboundKey(jobId);

        var entries = new NameValueEntry[]
        {
            new("type", "answer"),
            new("questionId", answer.QuestionId),
            new("answer", answer.Answer),
            new("comment", answer.Comment ?? ""),
            new("answeredAt", answer.AnsweredAt.ToString("O")),
            new("answeredBy", answer.AnsweredBy),
        };

        await db.StreamAddAsync(streamKey, entries, maxLength: StreamMaxLen, useApproximateMaxLength: true);
        await db.KeyExpireAsync(streamKey, KeyTtl);

        logger.LogDebug("Published answer for question {QuestionId} to {Stream}", answer.QuestionId, (string?)streamKey);
    }

    private static RedisKey OutboundKey(string jobId) => $"job:{jobId}:out";
    private static RedisKey InboundKey(string jobId) => $"job:{jobId}:in";

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;
}
