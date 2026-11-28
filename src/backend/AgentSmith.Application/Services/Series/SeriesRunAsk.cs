using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-27-3532b: a series-led ticket's run asks its series master, never the operator. The question its newest run is
/// parked on is the beat's reason, raised once per run and question (<see cref="KeyOf"/>, kept in the record's Raised once
/// the master judged it, and raised again when the operator-question slot frees while the master has not answered it).
/// </summary>
public static class SeriesRunAsk
{
    private const string Prefix = "question:";

    /// <summary>The once-key of the question the fact's newest run is parked on; null when it is parked on none.</summary>
    public static string? KeyOf(SeriesTicketFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return fact is { PendingQuestion: { } question, DialogueJobId: not null, LatestRunId: { } run }
            ? $"{Prefix}{run}:{question.QuestionId}" : null;
    }

    /// <summary>The question as the master is told it: its ticket, id, answerer, choices and recommendation, and how to answer.</summary>
    public static string Reason(SeriesTicketFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var question = fact.PendingQuestion ?? throw new ArgumentException("The fact's run is parked on no question.", nameof(fact));
        var key = fact.Ticket.Key;
        var choices = question.Choices?.Select(c => c.Label).ToList() ?? [];
        var text = $"{key}'s run is parked on a question to you ({string.Join(", ", question.AnswerersOrTarget)}; question id "
            + $"{question.QuestionId}): \"{question.Text}\""
            + (string.IsNullOrWhiteSpace(question.Context) ? string.Empty : $"\n{question.Context.Trim()}")
            + (choices.Count > 0 ? $"\nChoices: {string.Join(" · ", choices)}." : string.Empty)
            + (question.RecommendedIndex is { } at && at >= 0 && at < choices.Count ? $" It recommends: {choices[at]}." : string.Empty);
        return text + (question.AnswerersOrTarget.Contains(QuestionAnswerers.Environment)
            ? $"\nOnly the environment can answer it (a secret, a quota, a setting): forward it to the operator with ask_human "
                + $"(forTicket {key}), naming what to set or grant and where, with your recommendation. It has no default: it waits "
                + "for their reply, and you answer the run only once they replied."
            : $"\nAnswer it with tell_ticket {key} (questionId {question.QuestionId}) when you know or can look it up; otherwise ask "
                + $"the operator with ask_human (forTicket {key}) in your own words with your recommendation, and answer the run once they reply.");
    }
}
