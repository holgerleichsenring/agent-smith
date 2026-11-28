using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207c: how a series question reads in the conversation — the question, the recommendation and when
/// it is taken by default — and how its default is said. 2026-11-27-3532e: it is the master's plain chat message with one short
/// choices line — the page draws them as quick replies under the chat too, but a closed conversation, a pending decision
/// and Work it out show none — and a ticket's run never asks here (3532b).</summary>
public static class SeriesQuestionText
{
    /// <summary>The master's own question as the conversation reads it.</summary>
    public static string Asked(SeriesQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);
        var text = $"One decision only you can make:\n\n**{question.Text}**";
        if (question.Choices.Count > 0) text += $"\n\nChoices: {string.Join(" / ", question.Choices)}";
        if (question.Recommended is { } recommended) text += $"\n\nRecommended: {recommended}";
        return text + "\n\n" + (question.DeadlineAt is { } at
            ? $"If you don't answer, I take the recommendation at {at.UtcDateTime:HH:mm} UTC · or reply below."
            : question is { NoDefault: true, Recommended: not null } // 2026-11-28-b1db
                ? "Only you can do this, so nothing is taken by default: it waits for your answer · the series goes on meanwhile."
                : "Reply below; the series goes on meanwhile.");
    }

    public static string TakenByDefault(SeriesQuestion question, DateTimeOffset at) =>
        $"Taken by default at {at.UtcDateTime:HH:mm} UTC: {question.Recommended} — nobody answered \"{question.Text}\" by its deadline.";
}
