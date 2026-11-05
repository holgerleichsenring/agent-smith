using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// 2026-11-02-2b26d: the master's mid-run question on an inline-ticket run, which has no ticket to park on. It is
/// asked in the terminal through the dialogue gate, which prints, waits and records; the answer reaches the master
/// through <see cref="IMasterAnswerIntake.Accept"/> and the master is re-engaged. Nobody answering (stdin at end of
/// input, or the wait ran out) answers with a named default, so the master proceeds on its best reading; a second
/// such ask on the same spec ends the run naming the question instead of asking forever.
/// </summary>
public sealed class InlineMasterAsk(
    IDialogueAskGate askGate, IMasterAnswerIntake answerIntake, ILogger<InlineMasterAsk> logger)
{
    public const string NoAnswer = "No answer was given — proceed on your best reading and record the assumption.";
    private const string DefaultAsksKey = "InlineMasterDefaultAsks";

    public async Task<CommandResult> AskAsync(
        PipelineContext pipeline, PipelineCommand step, IReadOnlyList<PlanOpenQuestion> questions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(step);
        var question = MasterQuestionCheckpoint.Compose(questions, MasterQuestionCheckpoint.MintAskId())
            with { DefaultAnswer = NoAnswer };
        var outcome = await askGate.AskAsync(pipeline, question, cancellationToken);
        if (outcome.Checkpointed || outcome.Answer is null)
            return CommandResult.Fail("The master's question parked, and an inline run cannot be resumed");
        // The gate answers "system" with the question's default when nobody typed anything.
        if (outcome.Answer.AnsweredBy == "system" && CountDefault(pipeline, step.SpecId) > 1)
        {
            pipeline.Remove(ContextKeys.MasterOpenQuestions);
            return CommandResult.Fail($"The master asked again after no answer was given: {question.Text}");
        }
        var answer = Named(outcome.Answer);
        logger.LogInformation("The master's question on an inline run was answered: {Answer}", answer.Answer);
        answerIntake.Accept(pipeline, questions, answer);
        return MasterOpenQuestionsHandler.Reengage(step, answer);
    }

    // A blank answer is the default too; the gate's own note ("timeout") is not the master's to read.
    private static DialogAnswer Named(DialogAnswer answer) =>
        string.IsNullOrWhiteSpace(answer.Answer) || answer.AnsweredBy == "system"
            ? answer with { Answer = NoAnswer, Comment = null }
            : answer;

    private static int CountDefault(PipelineContext pipeline, string? specId)
    {
        var key = $"{DefaultAsksKey}:{specId}";
        var count = (pipeline.TryGet<int>(key, out var asked) ? asked : 0) + 1;
        pipeline.Set(key, count);
        return count;
    }
}
