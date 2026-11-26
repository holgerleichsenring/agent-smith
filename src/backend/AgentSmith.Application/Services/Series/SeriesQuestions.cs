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
/// <para>2026-11-25-e94ea: this is the conversation's one way to show a ticket's question. A relayed question whose
/// ticket's run no longer waits on it is dropped first, unanswered, and the next parked one is relayed — so an operator
/// message never reaches a run that left the question.</para>
/// </summary>
public sealed class SeriesQuestions(ISeriesRunStore store, IDialogueAnswerInbox inbox, SeriesBudgetQuestion budget) // 2026-11-25-9992g
{
    public async Task SettleAsync(SeriesBeat beat, int defaultMinutes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(beat);
        if (beat.Record.Question is { Answer: null } stale && Left(beat, stale))
        {
            await KeepAsync(beat, null, ct);
            if (beat.Record.Question is not null) return; // the drop was not kept: never answer the question the run left
        }
        if (beat.Record.Question is { Answer: null } waiting) await AnswerAsync(beat, waiting, ct);
        else await RelayAsync(beat, defaultMinutes, ct);
    }

    /// <summary>2026-11-25-e94ea: the asking ticket's run was read and is not parked on this question any more — resumed
    /// (answered on its own page), ended or parked on another. A ticket whose attempt was not read keeps its question.</summary>
    private static bool Left(SeriesBeat beat, SeriesQuestion question) =>
        question.TicketId is { } ticket && question.CheckpointQuestionId is { } asked
        && beat.Facts.FirstOrDefault(f => f.Ticket.TicketId == ticket) is { LatestStatus: not null } fact
        && fact.PendingQuestion?.QuestionId != asked;

    private async Task AnswerAsync(SeriesBeat beat, SeriesQuestion waiting, CancellationToken ct)
    {
        SeriesQuestion answered;
        if (beat.Messages.Count > 0) // 2026-11-25-9992g: only the operator's message answers — a comment's wake never does
        {
            answered = waiting with { Answer = beat.Messages[0].Text, AnsweredAt = beat.Messages[0].At, AnsweredBy = beat.Messages[0].Author };
            if (waiting.Budget && await budget.SettledAsync(beat, answered, ct)) return;
        }
        else if (!waiting.NoDefault && waiting.Recommended is { } recommended && waiting.DeadlineAt <= beat.Now)
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
            beat.Said.Add($"Passed your answer to {Key(beat, answered.TicketId)}'s run on \"{Headline(answered.Text)}\"; it goes on with it.");
        }
        else
        {
            beat.MasterAnswered = true; // 2026-11-25-e94ec: what the master makes of the answer is posted
            beat.Reasons.Add($"Your question \"{answered.Text}\" is answered{(answered.ByDefault ? " by default" : string.Empty)}: {answered.Answer}");
        }
        await KeepAsync(beat, answered with { Delivered = true }, ct);
    }

    private async Task RelayAsync(SeriesBeat beat, int defaultMinutes, CancellationToken ct)
    {
        if (beat.Record.IsOne) return; // 2026-11-25-96aea: a series of one's ticket keeps answering its questions on its page
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

    // 2026-11-25-e94ea: the question's first line names it in the delivered line, so a misrouted answer shows.
    private static string Headline(string text) =>
        text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? text.Trim();

    private async Task KeepAsync(SeriesBeat beat, SeriesQuestion? question, CancellationToken ct)
    {
        if (await store.UpdateAsync(beat.Record.Id, r => r with { Question = question }, ct) is { } kept) beat.Record = kept;
    }
}
