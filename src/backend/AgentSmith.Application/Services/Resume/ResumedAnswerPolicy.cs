using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;

namespace AgentSmith.Application.Services.Resume;

/// <summary>
/// 2026-08-25-a508: decides whether the answer a resume delivered belongs to the ask in hand.
/// <para>
/// p0327 re-keyed the delivered answer to whatever question the resumed run met first, because
/// a handler that mints its question id per execution (Approval) reaches the SAME ask under a
/// fresh id. That is right for the ask the run parked on and wrong for any other: a second,
/// genuinely new question would inherit the first question's answer and never be asked.
/// </para>
/// <para>
/// So the checkpointed question — staged by the resume reader — is the reference. Its id
/// identifies the ask when the id survived, its text when the id was re-minted. Anything else
/// is a new ask and publishes a question of its own.
/// </para>
/// </summary>
public static class ResumedAnswerPolicy
{
    public static bool TryConsume(
        PipelineContext pipeline, DialogQuestion question, out DialogAnswer? answer)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(question);
        answer = null;
        if (!pipeline.TryGet<DialogAnswer>(ContextKeys.ResumedDialogueAnswer, out var delivered)
            || delivered is null
            || !AnswersThisAsk(pipeline, question, delivered))
            return false;

        pipeline.Remove(ContextKeys.ResumedDialogueAnswer);
        answer = delivered with { QuestionId = question.QuestionId };
        return true;
    }

    private static bool AnswersThisAsk(
        PipelineContext pipeline, DialogQuestion question, DialogAnswer delivered)
    {
        if (string.Equals(delivered.QuestionId, question.QuestionId, StringComparison.Ordinal))
            return true;
        // No checkpointed question staged (a hand-built context, never a real resume) — there
        // is nothing to compare against, so the p0327 re-key stands.
        if (!pipeline.TryGet<DialogQuestion>(ContextKeys.DialogueQuestion, out var parked)
            || parked is null)
            return true;
        return string.Equals(parked.QuestionId, question.QuestionId, StringComparison.Ordinal)
            || string.Equals(parked.Text, question.Text, StringComparison.Ordinal);
    }
}
