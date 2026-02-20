using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Bus;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Bus;

/// <summary>
/// IProgressReporter implementation for K8s job mode.
/// Publishes progress, questions, done and error messages to Redis Streams.
/// Blocks on AskYesNoAsync until the dispatcher relays the user's answer back.
/// </summary>
public sealed class RedisProgressReporter(
    IMessageBus messageBus,
    string jobId,
    ILogger<RedisProgressReporter> logger) : IProgressReporter
{
    private static readonly TimeSpan AnswerTimeout = TimeSpan.FromMinutes(5);

    public async Task ReportProgressAsync(int step, int total, string commandName,
        CancellationToken cancellationToken = default)
    {
        var message = BusMessage.Progress(jobId, step, total, commandName);
        await messageBus.PublishAsync(message, cancellationToken);

        logger.LogDebug("Published progress {Step}/{Total} for job {JobId}", step, total, jobId);
    }

    public async Task<bool> AskYesNoAsync(string questionId, string text, bool defaultAnswer = true,
        CancellationToken cancellationToken = default)
    {
        var question = BusMessage.Question(jobId, questionId, text);
        await messageBus.PublishAsync(question, cancellationToken);

        logger.LogInformation(
            "Published question '{QuestionId}' for job {JobId}, waiting up to {Timeout}s for answer",
            questionId, jobId, AnswerTimeout.TotalSeconds);

        var answer = await messageBus.ReadAnswerAsync(jobId, AnswerTimeout, cancellationToken);

        if (answer is null)
        {
            logger.LogWarning(
                "No answer received for question '{QuestionId}' on job {JobId}, using default: {Default}",
                questionId, jobId, defaultAnswer);
            return defaultAnswer;
        }

        var content = answer.Content?.Trim().ToLowerInvariant();
        var result = content switch
        {
            "yes" or "y" or "true" or "1" => true,
            "no" or "n" or "false" or "0" => false,
            _ => defaultAnswer
        };

        logger.LogInformation(
            "Answer received for '{QuestionId}': '{Content}' → {Result}",
            questionId, answer.Content, result);

        return result;
    }

    public async Task ReportDoneAsync(string summary, string? prUrl = null,
        CancellationToken cancellationToken = default)
    {
        var message = BusMessage.Done(jobId, prUrl, summary);
        await messageBus.PublishAsync(message, cancellationToken);

        logger.LogInformation("Published Done for job {JobId}: {Summary}", jobId, summary);
    }

    public async Task ReportErrorAsync(string text,
        CancellationToken cancellationToken = default)
    {
        var message = BusMessage.Error(jobId, text);
        await messageBus.PublishAsync(message, cancellationToken);

        logger.LogError("Published Error for job {JobId}: {Text}", jobId, text);
    }
}
