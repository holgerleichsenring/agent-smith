using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Asks the human a question from a pipeline step (not from the agentic loop).
/// Publishes via IDialogueTransport, waits for the answer, records in IDialogueTrail.
/// Returns CommandResult.Fail on rejection (approval type with negative answer).
/// </summary>
public sealed class AskCommandHandler(
    IDialogueTransport dialogueTransport,
    IDialogueTrail dialogueTrail,
    IProgressReporter progressReporter,
    ILogger<AskCommandHandler> logger)
    : ICommandHandler<AskContext>
{
    private static readonly string[] RejectionAnswers = ["no", "n", "reject", "rejected", "deny", "denied"];

    public async Task<CommandResult> ExecuteAsync(
        AskContext context, CancellationToken cancellationToken)
    {
        var jobId = progressReporter.JobId;
        if (jobId is null)
        {
            logger.LogWarning("No job ID available, using default answer for question '{QuestionId}'",
                context.Question.QuestionId);

            var fallbackAnswer = new DialogAnswer(
                context.Question.QuestionId,
                context.Question.DefaultAnswer ?? "",
                "no job ID",
                DateTimeOffset.UtcNow,
                "system");

            await dialogueTrail.RecordAsync(context.Question, fallbackAnswer);
            context.Pipeline.Set(ContextKeys.DialogueAnswer, fallbackAnswer.Answer);
            return CommandResult.Ok($"No job ID, used default: {fallbackAnswer.Answer}");
        }

        logger.LogInformation("Asking human: {Text}", context.Question.Text);
        await dialogueTransport.PublishQuestionAsync(jobId, context.Question, cancellationToken);

        var answer = await dialogueTransport.WaitForAnswerAsync(
            jobId, context.Question.QuestionId,
            context.Question.Timeout, cancellationToken);

        if (answer is null)
        {
            var defaultAnswer = context.Question.DefaultAnswer ?? "";
            answer = new DialogAnswer(
                context.Question.QuestionId, defaultAnswer,
                "timeout", DateTimeOffset.UtcNow, "system");

            logger.LogWarning("Question '{QuestionId}' timed out, using default: {Default}",
                context.Question.QuestionId, defaultAnswer);
        }
        else
        {
            logger.LogInformation("Received answer for '{QuestionId}': {Answer}",
                context.Question.QuestionId, answer.Answer);
        }

        await dialogueTrail.RecordAsync(context.Question, answer);
        context.Pipeline.Set(ContextKeys.DialogueAnswer, answer.Answer);

        if (context.Question.Type == QuestionType.Approval &&
            RejectionAnswers.Contains(answer.Answer.Trim().ToLowerInvariant()))
        {
            return CommandResult.Fail($"Rejected by {answer.AnsweredBy}: {answer.Answer}");
        }

        return CommandResult.Ok($"Answer: {answer.Answer}");
    }
}
