using AgentSmith.Contracts.Dialogue;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services;

/// <summary>
/// Interactive console-based dialogue transport for local CLI mode.
/// Presents typed questions on the console and reads answers from stdin.
/// Used when running locally (not in headless/container mode).
/// </summary>
public sealed class ConsoleDialogueTransport(
    TextReader input,
    TextWriter output,
    ILogger<ConsoleDialogueTransport> logger) : IDialogueTransport
{
    public Task PublishQuestionAsync(string jobId, DialogQuestion question, CancellationToken cancellationToken)
    {
        // Store question so WaitForAnswerAsync can display and prompt for it
        _pendingQuestions[BuildKey(jobId, question.QuestionId)] = question;
        return Task.CompletedTask;
    }

    public async Task<DialogAnswer?> WaitForAnswerAsync(
        string jobId, string questionId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        // We need the question to display it — retrieve from the publish call is not possible,
        // so we rely on the caller pattern: PublishQuestion then WaitForAnswer.
        // For console mode, the question display + answer collection happens here via a stored question.
        // However, the interface doesn't pass the question to WaitForAnswer.
        // We store published questions and retrieve them here.

        if (!_pendingQuestions.TryRemove(BuildKey(jobId, questionId), out var question))
        {
            logger.LogWarning("No pending question found for {QuestionId} on job {JobId}", questionId, jobId);
            return null;
        }

        return await PromptAndReadAsync(jobId, question, timeout, cancellationToken);
    }

    public Task PublishAnswerAsync(string jobId, DialogAnswer answer, CancellationToken cancellationToken)
    {
        // No-op in console mode — answers go directly back from WaitForAnswerAsync
        return Task.CompletedTask;
    }

    // Store questions from PublishQuestionAsync so WaitForAnswerAsync can display them
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DialogQuestion> _pendingQuestions = new();

    private static string BuildKey(string jobId, string questionId) => $"{jobId}:{questionId}";

    private async Task<DialogAnswer?> PromptAndReadAsync(
        string jobId, DialogQuestion question, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var prompt = FormatPrompt(question);
        output.WriteLine();
        output.Write(prompt);

        if (question.Type == QuestionType.Info)
        {
            output.WriteLine();
            return null;
        }

        var readTask = Task.Run(() => input.ReadLine(), CancellationToken.None);
        var delayTask = Task.Delay(timeout, cancellationToken);

        var completed = await Task.WhenAny(readTask, delayTask);

        if (completed == delayTask)
        {
            output.WriteLine();
            output.WriteLine("[Timed out waiting for answer]");
            logger.LogWarning("Console input timed out for question {QuestionId} on job {JobId}", question.QuestionId, jobId);
            return null;
        }

        var answer = await readTask;

        if (answer is null)
        {
            logger.LogDebug("Received null input for question {QuestionId}", question.QuestionId);
            return null;
        }

        var resolved = ResolveAnswer(question, answer);
        logger.LogDebug("Received answer for question {QuestionId}: {Answer}", question.QuestionId, resolved);

        return new DialogAnswer(
            question.QuestionId,
            resolved,
            null,
            DateTimeOffset.UtcNow,
            "console-user");
    }

    internal static string FormatPrompt(DialogQuestion question)
    {
        var sb = new System.Text.StringBuilder();

        if (!string.IsNullOrEmpty(question.Context))
        {
            sb.AppendLine(question.Context);
            sb.AppendLine();
        }

        sb.AppendLine(question.Text);

        switch (question.Type)
        {
            case QuestionType.Confirmation:
                var defaultYn = string.Equals(question.DefaultAnswer, "yes", StringComparison.OrdinalIgnoreCase)
                    ? "Y/n" : "y/N";
                sb.Append($"[{defaultYn}]: ");
                break;

            case QuestionType.Choice when question.Choices is { Count: > 0 }:
                for (var i = 0; i < question.Choices.Count; i++)
                {
                    sb.AppendLine($"  {i + 1}. {question.Choices[i]}");
                }
                sb.Append("Enter number: ");
                break;

            case QuestionType.FreeText:
                sb.Append("> ");
                break;

            case QuestionType.Approval:
                sb.Append("[A]pprove / [R]eject: ");
                break;

            case QuestionType.Info:
                // No prompt needed
                break;
        }

        return sb.ToString();
    }

    private static string ResolveAnswer(DialogQuestion question, string raw)
    {
        var trimmed = raw.Trim();

        switch (question.Type)
        {
            case QuestionType.Confirmation:
                if (string.IsNullOrEmpty(trimmed))
                    return question.DefaultAnswer ?? "yes";
                return trimmed.StartsWith("y", StringComparison.OrdinalIgnoreCase) ? "yes" : "no";

            case QuestionType.Choice when question.Choices is { Count: > 0 }:
                if (int.TryParse(trimmed, out var idx) && idx >= 1 && idx <= question.Choices.Count)
                    return question.Choices[idx - 1];
                return trimmed;

            case QuestionType.Approval:
                if (string.IsNullOrEmpty(trimmed))
                    return question.DefaultAnswer ?? "reject";
                return trimmed.StartsWith("a", StringComparison.OrdinalIgnoreCase) ? "approve" : "reject";

            default:
                return string.IsNullOrEmpty(trimmed) ? question.DefaultAnswer ?? "" : trimmed;
        }
    }
}
