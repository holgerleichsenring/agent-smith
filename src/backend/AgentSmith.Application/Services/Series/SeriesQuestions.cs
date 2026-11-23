using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207c: the one question a series waits on, settled at the start of each beat. A ticket's run parked on
/// a master question is relayed into the conversation when none waits; an operator message answers the waiting one;
/// past its deadline a question with a recommendation takes it, and the conversation says so. A relayed answer goes to
/// the parked run's inbox, which the resume sweep picks up; the master's own answer goes to the master.
/// <para>2026-11-23-31dch: a plan question is relayed first and says it is about the plan; a question only the environment
/// can answer is the admin's and is not relayed. The answer is the poster's, delivered through the same inbox the ticket's
/// answer uses, so the resume and the in-run intake stay the one path.</para>
/// </summary>
public sealed class SeriesQuestions(ISeriesRunStore store, IDialogueAnswerInbox inbox)
{
    public async Task SettleAsync(SeriesBeat beat, int defaultMinutes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(beat);
        if (beat.Record.Question is { Answer: null } waiting) await AnswerAsync(beat, waiting, ct);
        else await RelayAsync(beat, defaultMinutes, ct);
    }

    private async Task AnswerAsync(SeriesBeat beat, SeriesQuestion waiting, CancellationToken ct)
    {
        SeriesQuestion answered;
        if (beat.Messages.Count > 0)
            answered = waiting with { Answer = beat.Messages[0].Text, AnsweredAt = beat.Messages[0].At, AnsweredBy = beat.Messages[0].Author };
        else if (waiting.Recommended is { } recommended && waiting.DeadlineAt <= beat.Now)
        {
            answered = waiting with { Answer = recommended, AnsweredAt = beat.Now, ByDefault = true };
            beat.Said.Add(SeriesQuestionText.TakenByDefault(waiting, beat.Now));
        }
        else return;
        await DeliverAsync(beat, answered, ct);
    }

    private async Task DeliverAsync(SeriesBeat beat, SeriesQuestion answered, CancellationToken ct)
    {
        if (answered.DialogueJobId is { } job && answered.CheckpointQuestionId is { } id)
        {
            await inbox.TryDeliverAsync(job, new DialogAnswer(id, answered.Answer!, null, beat.Now, answered.AnsweredBy ?? beat.Record.Principal), ct);
            beat.Said.Add($"Passed the answer to {Key(beat, answered.TicketId)}'s run; it goes on with it.");
        }
        else beat.Reasons.Add($"Your question \"{answered.Text}\" is answered{(answered.ByDefault ? " by default" : string.Empty)}: {answered.Answer}");
        await KeepAsync(beat, answered with { Delivered = true }, ct);
    }

    private async Task RelayAsync(SeriesBeat beat, int defaultMinutes, CancellationToken ct)
    {
        var parked = beat.Facts.Where(f => f.PendingQuestion is { } q && f.DialogueJobId is not null
                && q.QuestionId != beat.Record.Question?.CheckpointQuestionId && IsRelayed(q))
            .OrderByDescending(f => IsPlan(f.PendingQuestion!)).FirstOrDefault();
        if (parked?.PendingQuestion is not { } question) return;
        var choices = question.Choices?.Select(c => c.Label).ToList() ?? [];
        var relayed = Ask(question.Text, choices, question.RecommendedIndex, beat.Now, defaultMinutes) with
        {
            TicketId = parked.Ticket.TicketId, DialogueJobId = parked.DialogueJobId, CheckpointQuestionId = question.QuestionId,
        };
        beat.Said.Add(SeriesQuestionText.Asked(relayed, parked.Ticket.Key, IsPlan(question)));
        await KeepAsync(beat, relayed, ct);
    }

    /// <summary>A new question; with a recommendation it is taken by default after the configured minutes.</summary>
    public static SeriesQuestion Ask(string text, IReadOnlyList<string> choices, int? recommended, DateTimeOffset now, int defaultMinutes)
    {
        var valid = recommended is { } i && i >= 0 && i < choices.Count ? recommended : null;
        return new SeriesQuestion(Guid.NewGuid().ToString("N"), text, choices, valid, now,
            valid is null ? null : now.AddMinutes(defaultMinutes));
    }

    // 2026-11-23-31dch: the environment's question is the admin's; every other one a person in the conversation can answer.
    private static bool IsRelayed(DialogQuestion question) =>
        question.AnswerersOrTarget.Any(a => a != Domain.Models.QuestionAnswerers.Environment);

    private static bool IsPlan(DialogQuestion question) =>
        question.AnswerersOrTarget.Contains(Domain.Models.QuestionAnswerers.Plan);

    private static string Key(SeriesBeat beat, string? ticketId) =>
        beat.Facts.FirstOrDefault(f => f.Ticket.TicketId == ticketId)?.Ticket.Key ?? ticketId ?? "the ticket";

    private async Task KeepAsync(SeriesBeat beat, SeriesQuestion question, CancellationToken ct)
    {
        if (await store.UpdateAsync(beat.Record.Id, r => r with { Question = question }, ct) is { } kept) beat.Record = kept;
    }
}
