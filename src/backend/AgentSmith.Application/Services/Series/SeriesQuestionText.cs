using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207c: how a series question reads in the conversation — the question, its choices, the
/// recommendation and when it is taken by default — and how its default is said.</summary>
public static class SeriesQuestionText
{
    public static string Asked(SeriesQuestion question, string? ticketKey)
    {
        ArgumentNullException.ThrowIfNull(question);
        var text = (ticketKey is null ? "One decision only you can make:" : $"{ticketKey} asks a decision only you can make:")
            + $"\n\n**{question.Text}**";
        if (question.Choices.Count > 0) text += $"\n\nChoices: {string.Join(" · ", question.Choices)}";
        if (question.Recommended is { } recommended) text += $"\n\nRecommended: {recommended}";
        return text + "\n\n" + (question.DeadlineAt is { } at
            ? $"If you don't answer, I take the recommendation at {at.UtcDateTime:HH:mm} UTC · or reply below."
            : "Reply below; the series goes on meanwhile.");
    }

    public static string TakenByDefault(SeriesQuestion question, DateTimeOffset at) =>
        $"Taken by default at {at.UtcDateTime:HH:mm} UTC: {question.Recommended} — nobody answered \"{question.Text}\" by its deadline.";
}
