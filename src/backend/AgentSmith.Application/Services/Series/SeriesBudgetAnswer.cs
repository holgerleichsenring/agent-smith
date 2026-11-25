using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-25-9992g: which choice an answer to the budget question is — only an answer that IS a choice: its label (what a
/// button sends) or the bare word "raise" or "stop", ignoring case, surrounding blanks and punctuation. "Stop restarting CI
/// on SCRUM-12" is an instruction to the master, not a choice, and stops nothing. A bare number is no choice either: whether
/// "1" means the first or the second is a guess, and stopping a series on a guess is not acceptable.
/// </summary>
public static class SeriesBudgetAnswer
{
    /// <summary>0 for raise, 1 for stop; null for any other answer.</summary>
    public static int? Choice(SeriesQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);
        var answer = Plain(question.Answer);
        if (answer.Length == 0) return null;
        for (var i = 0; i < Math.Min(2, question.Choices.Count); i++)
            if (answer == (i == 0 ? "raise" : "stop") || answer == Plain(question.Choices[i])) return i;
        return null;
    }

    private static string Plain(string? text) =>
        (text ?? string.Empty).Trim().Trim('.', '!', '?', ',', ';', ':', '"', '\'', '*', '`', ' ').ToLowerInvariant();
}
