using AgentSmith.Application.Services.Resume;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// 2026-09-03-3c07: the answer a resume delivers reaches the master that asked.
/// <para>
/// The resumed answer's only reader was the dialogue ask gate, and the master's park never
/// goes through the gate — so even with the park marker fixed the resumed MasterOpenQuestions
/// step would have posted the same question again. This consumes the delivered answer by
/// the same rule the gate applies (<see cref="ResumedAnswerPolicy"/>: the ask the run parked
/// on, not any other), records it on the dialogue trail like the gate does, and publishes it
/// under the key the phase prompt already renders as the operator's answers. No second inbox.
/// </para>
/// </summary>
public sealed class MasterAnswerIntake(
    IDialogueTrail dialogueTrail,
    ILogger<MasterAnswerIntake> logger) : IMasterAnswerIntake
{
    public async Task<DialogAnswer?> TryDeliverAsync(
        PipelineContext pipeline, IReadOnlyList<PlanOpenQuestion> questions)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(questions);
        if (questions.Count == 0) return null;

        // Composed under a fresh id, exactly as the checkpoint composed it: the policy then
        // matches by TEXT against the ask the run parked on, and an answer to any other ask
        // is left alone (2026-08-25-a508). Minting the parked id here instead would make
        // the id comparison true for every question, whatever it asks.
        var question = MasterQuestionCheckpoint.Compose(questions, MasterQuestionCheckpoint.MintAskId());
        if (!ResumedAnswerPolicy.TryConsume(pipeline, question, out var answer) || answer is null)
            return null;

        pipeline.Set(ContextKeys.PlanAnswers, Answered(pipeline, questions, answer));
        // The question is answered: neither this step nor a later pass may park on it again.
        pipeline.Remove(ContextKeys.MasterOpenQuestions);
        pipeline.Remove(ContextKeys.OpenQuestionsAwaitingAnswer);
        await dialogueTrail.RecordAsync(question, answer);
        logger.LogInformation(
            "Operator answered the master's question '{QuestionId}' ({By}) — re-engaging the master",
            answer.QuestionId, answer.AnsweredBy);
        return answer;
    }

    // One reply covers every question of the ask, exactly as the ticket comment asks for it;
    // answers an earlier round already carried stay in place.
    private static Dictionary<string, string> Answered(
        PipelineContext pipeline, IReadOnlyList<PlanOpenQuestion> questions, DialogAnswer answer)
    {
        var answers = pipeline.TryGet<Dictionary<string, string>>(ContextKeys.PlanAnswers, out var prior)
            && prior is not null
            ? new Dictionary<string, string>(prior, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
        var text = string.IsNullOrWhiteSpace(answer.Comment)
            ? answer.Answer
            : $"{answer.Answer} ({answer.Comment})";
        foreach (var question in questions) answers[question.Id] = text;
        return answers;
    }
}
