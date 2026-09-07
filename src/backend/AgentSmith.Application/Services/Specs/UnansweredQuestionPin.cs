using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// Turns an unanswered question hand-back into the answer the derivation reads. A
/// question parks the ticket naming both readings and the one the run would take; if
/// the ticket comes back with nobody having answered, the taken reading is pinned into
/// the derivation prompt as if the author had confirmed it, and the cut proceeds.
/// <para>
/// "Unanswered" is mechanical: no comment by anyone but us after our question, read
/// from the thread the run already carries. The branch is never the signal — every
/// derivation commits a fresh revision, so a sha comparison would call every
/// re-trigger progress. A thread that holds comments by others but not our question
/// is read as answered: the model then sees the thread and asks again if it must,
/// which costs one park, whereas a wrong proceed costs the work.
/// </para>
/// </summary>
public sealed class UnansweredQuestionPin(ILogger<UnansweredQuestionPin> logger)
{
    /// <summary>
    /// Sets <see cref="ContextKeys.SpecQuestionPin"/> and returns the question when the
    /// previous set's hand-back is a question nobody answered; null otherwise.
    /// </summary>
    public UnansweredQuestion? Pin(SpecSet? previous, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var question = previous?.Handback;
        if (question is null || question.TakenReading is not { } taken) return null;
        var comments = pipeline.TryGet<IReadOnlyList<TicketComment>>(
            ContextKeys.TicketComments, out var c) ? c : null;
        if (IsAnswered(comments))
        {
            logger.LogInformation("The question on {Key} was answered — deriving on the answer", previous!.Key);
            return null;
        }
        var unanswered = new UnansweredQuestion(
            question, SpecHandbackComment.ReadingLabel(question.Taken), taken);
        pipeline.Set(ContextKeys.SpecQuestionPin, UnansweredQuestionPromptSection.Render(unanswered));
        logger.LogInformation(
            "The question on {Key} was left unanswered — deriving on reading {Label}",
            previous!.Key, unanswered.TakenLabel);
        return unanswered;
    }

    private static bool IsAnswered(IReadOnlyList<TicketComment>? comments)
    {
        if (comments is null || comments.Count == 0) return false;
        var ordered = comments.OrderBy(c => c.CreatedAt).ToList();
        var asked = ordered.LastOrDefault(IsOurQuestion);
        var after = asked is null ? ordered : ordered.Where(c => c.CreatedAt > asked.CreatedAt);
        return after.Any(c => !OwnTicketComment.IsOurs(c));
    }

    private static bool IsOurQuestion(TicketComment comment) =>
        OwnTicketComment.IsOurs(comment)
        && comment.Body.Contains(SpecHandbackComment.QuestionMarker, StringComparison.OrdinalIgnoreCase);
}
