using System.Text.Json.Nodes;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Handles the "ask_human" tool: publishes a question via dialogue transport,
/// waits for an answer (or times out), and records the exchange.
/// </summary>
internal sealed class HumanQuestionToolHandler(
    IDialogueTransport? dialogueTransport,
    IDialogueTrail? dialogueTrail,
    string? jobId,
    ILogger logger,
    IProgressReporter? progressReporter)
{
    private static readonly TimeSpan DefaultQuestionTimeout = TimeSpan.FromMinutes(5);

    public async Task<string> HandleAsync(JsonNode? input)
    {
        if (dialogueTransport is null || jobId is null)
            return "Error: Dialogue transport not configured. Cannot ask human.";

        var questionTypeStr = ToolParams.GetString(input, "question_type");
        var text = ToolParams.GetString(input, "text");
        var context = ToolParams.GetString(input, "context");
        var defaultAnswer = ToolParams.GetString(input, "default_answer");
        var choicesNode = input?["choices"];

        var normalizedType = questionTypeStr.Replace("_", "");
        if (!Enum.TryParse<QuestionType>(normalizedType, ignoreCase: true, out var questionType))
            return $"Error: Invalid question_type '{questionTypeStr}'.";

        List<string>? choices = null;
        if (choicesNode is JsonArray arr)
            choices = arr.Select(c => c?.GetValue<string>() ?? "").Where(c => c.Length > 0).ToList();

        var questionId = Guid.NewGuid().ToString("N");
        var question = new DialogQuestion(
            questionId, questionType, text, context, choices?.AsReadOnly(),
            defaultAnswer, DefaultQuestionTimeout);

        ReportDetail($"\u2753 Asking human: {text}");
        await dialogueTransport.PublishQuestionAsync(jobId, question, CancellationToken.None);

        var answer = await dialogueTransport.WaitForAnswerAsync(
            jobId, questionId, DefaultQuestionTimeout, CancellationToken.None);

        string answerText;
        if (answer is null)
        {
            answerText = defaultAnswer;
            answer = new DialogAnswer(questionId, defaultAnswer, "timeout", DateTimeOffset.UtcNow, "system");
            logger.LogWarning("Question '{QuestionId}' timed out, using default answer: {Default}", questionId, defaultAnswer);
        }
        else
        {
            answerText = answer.Answer;
            logger.LogInformation("Received answer for '{QuestionId}': {Answer}", questionId, answerText);
        }

        if (dialogueTrail is not null)
            await dialogueTrail.RecordAsync(question, answer);

        var timedOut = answer.Comment == "timeout";
        return timedOut
            ? $"Answer (timeout, used default): {answerText}"
            : $"Answer: {answerText}";
    }

    private void ReportDetail(string text)
    {
        try { progressReporter?.ReportDetailAsync(text, CancellationToken.None).GetAwaiter().GetResult(); }
        catch (Exception ex) { logger.LogDebug(ex, "Detail reporting failed"); }
    }
}
