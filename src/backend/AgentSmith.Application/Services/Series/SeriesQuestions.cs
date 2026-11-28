using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207c: the one question a series waits on, settled at the start of each beat: an operator message answers
/// the waiting one; past its deadline a question with a recommendation takes it, and the conversation says so. The answer
/// goes to the master, and counts as delivered only once the master judged it (SeriesMasterHandler), so a failed loop is
/// handed it again.
/// <para>2026-11-27-3532b: a series-led ticket's run asks its master, never the operator: the question its run is parked on
/// is the beat's reason, once per run and question (<see cref="SeriesRunAsk"/>), while the series leads (<see cref="SeriesLeads"/>).
/// The master answers it with tell_ticket or carries it to the operator with ask_human, one question at a time — so when that
/// slot frees, every run question still parked that the master neither answered nor carried since is raised again. A ticket's
/// question relayed into the chat before is dropped at the next beat, unanswered — the master is asked it instead.</para>
/// </summary>
public sealed class SeriesQuestions(ISeriesRunStore store, SeriesBudgetQuestion budget, // 2026-11-25-9992g
    DialogueGlobalConfig dialogue) // 2026-11-28-d1ffb: the store's current dialogue: block, so a dashboard change reaches the next beat
{
    private const string TellTool = "tell_ticket";

    public async Task SettleAsync(SeriesBeat beat, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(beat);
        var waiting = beat.Record.Question is { Answer: null } open ? open : null;
        if (waiting is { TicketId: not null }) await KeepAsync(beat, null, ct);
        else if (waiting is not null) await AnswerAsync(beat, waiting, ct);
        else if (beat.Record.Question is { Answer: not null, Delivered: false, TicketId: null } handed) Hand(beat, handed);
        if (waiting is not null && beat.Record.Question is not { Answer: null }) await ReopenAsync(beat, waiting, ct);
        Raise(beat);
    }

    /// <summary>A new question; with a recommendation it is taken by default after the configured minutes — unless
    /// <paramref name="noDefault"/> (2026-11-28-b1db: only a person or the environment can carry it out), when it keeps its
    /// recommendation for the reader and has no deadline. 2026-11-28-d1ffb: null minutes — defaults are off — gives none either.</summary>
    public static SeriesQuestion Ask(string text, IReadOnlyList<string> choices, int? recommended, DateTimeOffset now, int? defaultMinutes,
        bool noDefault = false)
    {
        var valid = recommended is { } i && i >= 0 && i < choices.Count ? recommended : null;
        return new SeriesQuestion(Guid.NewGuid().ToString("N"), text, choices, valid, now,
            valid is null || noDefault || defaultMinutes is not { } minutes ? null : now.AddMinutes(minutes)) { NoDefault = noDefault };
    }

    /// <summary>2026-11-28-b1db: the question carries a ticket's run question only the environment can answer.</summary>
    public static bool CarriesEnvironment(SeriesQuestion question, SeriesTicketFact fact) =>
        Carried(question, fact) && fact.PendingQuestion?.AnswerersOrTarget.Contains(QuestionAnswerers.Environment) == true;

    private void Raise(SeriesBeat beat)
    {
        if (!budget.Leads(beat.Record)) return; // a series of one, or a master that cannot act: the ticket page answers it
        foreach (var fact in beat.Facts)
        {
            if (SeriesRunAsk.KeyOf(fact) is not { } key || beat.Record.Raised.Contains(key) || beat.Raised.Contains(key)) continue;
            beat.Raised.Add(key);
            beat.Reasons.Add(SeriesRunAsk.Reason(fact));
        }
    }

    // The slot freed: a raised run question still parked, neither carried by the freed question nor told since it was asked.
    private async Task ReopenAsync(SeriesBeat beat, SeriesQuestion freed, CancellationToken ct)
    {
        List<string> keys = [.. beat.Facts.Where(f => !Carried(freed, f) && !Told(beat.Record, f, freed.AskedAt))
            .Select(SeriesRunAsk.KeyOf).OfType<string>().Where(beat.Record.Raised.Contains)];
        if (keys.Count == 0) return;
        if (await store.UpdateAsync(beat.Record.Id, r => r with { Raised = [.. r.Raised.Where(k => !keys.Contains(k))] }, ct) is { } kept)
            beat.Record = kept;
    }

    private static bool Carried(SeriesQuestion question, SeriesTicketFact fact) =>
        question.ForRunId is not null && question.ForRunId == fact.LatestRunId && question.ForQuestionId == fact.PendingQuestion?.QuestionId;

    private static bool Told(SeriesRunRecord record, SeriesTicketFact fact, DateTimeOffset since) =>
        record.Acts.Any(a => a.Tool == TellTool && (a.StartedAt ?? a.At) >= since && a.Ticket?.TrimStart('#') is { } named
            && (named == fact.Ticket.TicketId || named == fact.Ticket.Key.TrimStart('#')));

    private async Task AnswerAsync(SeriesBeat beat, SeriesQuestion waiting, CancellationToken ct)
    {
        SeriesQuestion answered;
        if (beat.Messages.Count > 0) // 2026-11-25-9992g: only the operator's message answers — a comment's wake never does
        {
            answered = waiting with { Answer = beat.Messages[0].Text, AnsweredAt = beat.Messages[0].At, AnsweredBy = beat.Messages[0].Author };
            if (waiting.Budget && await budget.SettledAsync(beat, answered, ct)) return;
        }
        else if (waiting.DeadlineAt is not null && !dialogue.QuestionDefaultEnabled)
        {
            // 2026-11-28-d1ffb: asked with a deadline before defaults were turned off — it waits for the operator, and says so.
            await KeepAsync(beat, waiting with { DeadlineAt = null }, ct);
            beat.Said.Add(SeriesQuestionText.DefaultsOff(waiting));
            return;
        }
        else if (!waiting.NoDefault && waiting.DeadlineAt is not null && beat.Facts.Any(f => CarriesEnvironment(waiting, f)))
        {
            // 2026-11-28-b1db: stored with a deadline before — only the environment can do it, so it waits for the operator.
            await KeepAsync(beat, waiting with { NoDefault = true, DeadlineAt = null }, ct);
            return;
        }
        else if (!waiting.NoDefault && waiting.Recommended is { } recommended && waiting.DeadlineAt <= beat.Now)
        {
            answered = waiting with { Answer = recommended, AnsweredAt = beat.Now, ByDefault = true };
            beat.Said.Add(SeriesQuestionText.TakenByDefault(waiting, beat.Now));
        }
        else return;
        Hand(beat, answered);
        await KeepAsync(beat, answered, ct);
    }

    // 2026-11-25-e94ec: what the master makes of the answer is posted; a question carried for a run names it and its question.
    private static void Hand(SeriesBeat beat, SeriesQuestion answered)
    {
        beat.MasterAnswered = true;
        beat.Reasons.Add(SeriesQuestionText.Handed(answered));
    }

    private async Task KeepAsync(SeriesBeat beat, SeriesQuestion? question, CancellationToken ct)
    {
        if (await store.UpdateAsync(beat.Record.Id, r => r with { Question = question }, ct) is { } kept) beat.Record = kept;
    }
}
