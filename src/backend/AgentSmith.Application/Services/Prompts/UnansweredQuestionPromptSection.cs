using AgentSmith.Application.Services.Specs;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// Renders an unanswered question hand-back as the derivation's answer: both readings
/// as the ticket listed them, and the taken one stated as confirmed. The derivation
/// then cuts specs under it instead of asking the same question again.
/// </summary>
public static class UnansweredQuestionPromptSection
{
    public static string Render(UnansweredQuestion unanswered)
    {
        ArgumentNullException.ThrowIfNull(unanswered);
        if (unanswered.Question.QuestionKind == Contracts.Specs.SpecQuestionKind.ReviewObjection) return Objection(unanswered);
        var readings = string.Join("\n", unanswered.Question.Readings.Select(
            (reading, i) => $"- {SpecHandbackComment.ReadingLabel(i)} {reading}"));
        return $"""

            ## The question from the last run was left unanswered
            The last run handed this ticket back because it reads two ways:
            {readings}

            Nobody answered, and the ticket was re-triggered. Reading {unanswered.TakenLabel} is
            therefore the answer: treat it as confirmed by the ticket author, cut specs under it,
            and state it as an assumption in the goal or done-list. Do not ask this question again.
            """;
    }

    // 2026-10-13-8762h: the objection was asked once; silence builds the spec as it stands.
    private static string Objection(UnansweredQuestion unanswered) => $"""

        ## A reviewer's objection the author did not answer
        The last run asked the ticket's author about this objection:
        {unanswered.Question.Reason}

        The author was asked about this objection and did not answer; keep the spec as it stands, do not change it for this objection and do not raise it again.
        The run proceeds on reading {unanswered.TakenLabel}: {unanswered.TakenReading}.
        """;
}
