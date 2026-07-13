using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Asks the human a question from a pipeline step (not from the agentic loop).
/// p0327: the wait is hybrid — hot in-memory below the configured threshold,
/// then the run checkpoints and parks (IDialogueAskGate owns the mechanics,
/// including consuming a resume-delivered answer on re-entry). Returns
/// CommandResult.Fail on rejection (approval type with negative answer).
/// </summary>
public sealed class AskCommandHandler(
    IDialogueAskGate askGate,
    ILogger<AskCommandHandler> logger)
    : ICommandHandler<AskContext>
{
    internal static readonly string[] RejectionAnswers = ["no", "n", "reject", "rejected", "deny", "denied"];

    public async Task<CommandResult> ExecuteAsync(
        AskContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Asking human: {Text}", context.Question.Text);
        var outcome = await askGate.AskAsync(context.Pipeline, context.Question, cancellationToken);

        if (outcome.Checkpointed)
            return CommandResult.Ok(
                $"Parked: waiting for the operator's answer to '{context.Question.QuestionId}' (checkpointed)");

        var answer = outcome.Answer!;
        logger.LogInformation("Received answer for '{QuestionId}': {Answer}",
            context.Question.QuestionId, answer.Answer);
        context.Pipeline.Set(ContextKeys.DialogueAnswer, answer.Answer);

        if (context.Question.Type == QuestionType.Approval &&
            RejectionAnswers.Contains(answer.Answer.Trim().ToLowerInvariant()))
        {
            return CommandResult.Fail($"Rejected by {answer.AnsweredBy}: {answer.Answer}");
        }

        // A gate-synthesized fallback (timeout / no dialogue identity) says so —
        // the operator must see the run proceeded on a default, not a human reply.
        return answer.AnsweredBy == "system"
            ? CommandResult.Ok($"Used default answer ({answer.Comment}): {answer.Answer}")
            : CommandResult.Ok($"Answer: {answer.Answer}");
    }
}
