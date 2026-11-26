using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-25-9992g: the series asks the conversation before its master's budget is spent — in any beat once no more than
/// dialogue.series_master_budget_ask_usd is left: raise the budget by dialogue.series_master_budget_usd, or stop the
/// series. "Raise" is recommended so the page draws the choices as buttons, but the question has no deadline: a budget is
/// never raised, nor a series stopped, by default. It goes before a relayed ticket question (which is relayed again once
/// it is answered) and after the master's own. An answer that is exactly a choice ("raise", "stop", or a label) settles
/// it; any other goes to the master, and the question is asked again in a later beat.
/// </summary>
public sealed class SeriesBudgetQuestion(ISeriesRunStore store, AgentSmithConfig config)
{
    private DialogueGlobalConfig Dialogue => config.Dialogue;

    public async Task AskIfLowAsync(SeriesBeat beat, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(beat);
        var record = beat.Record;
        var left = record.BudgetUsd(Dialogue.SeriesMasterBudgetUsd) - record.MasterCostUsd;
        if (!record.IsActive || beat.BudgetHeard || record.BudgetAsked || Blocked(record) || left > Dialogue.SeriesMasterBudgetAskUsd) return;
        if (string.IsNullOrEmpty(record.Conversation)) return; // 2026-11-25-96aea: a series of one without a conversation has nobody to ask
        var asked = Question(record, left, beat.Now);
        var kept = await store.UpdateAsync(record.Id,
            r => r.BudgetAsked || Blocked(r) ? r : r with { Question = asked, BudgetAsked = true }, ct);
        if (kept is null) return;
        beat.Record = kept;
        if (kept.Question?.Id == asked.Id) beat.Said.Add(SeriesQuestionText.Asked(asked, null));
    }

    /// <summary>Settles an answer that is exactly one of the choices; for any other it clears the ask, so a later beat
    /// asks again, and answers false — that answer goes to the master.</summary>
    public async Task<bool> SettledAsync(SeriesBeat beat, SeriesQuestion answered, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(answered);
        var choice = SeriesBudgetAnswer.Choice(answered);
        if (choice is null)
        {
            beat.BudgetHeard = true; // asked again in a later beat, after the master answered this one
            if (await store.UpdateAsync(beat.Record.Id, r => r with { BudgetAsked = false }, ct) is { } open) beat.Record = open;
            return false;
        }
        await KeepAsync(beat, answered with { Delivered = true }, raise: choice == 0, ct);
        return true;
    }

    private async Task KeepAsync(SeriesBeat beat, SeriesQuestion delivered, bool raise, CancellationToken ct)
    {
        var kept = await store.UpdateAsync(beat.Record.Id, r => raise
            ? r with { Question = delivered, BudgetAsked = false, BudgetSaid = false, BudgetRaisedUsd = r.BudgetRaisedUsd + Dialogue.SeriesMasterBudgetUsd }
            : r with { Question = delivered, State = SeriesRunStates.Stopped, FinishedAt = beat.Now }, ct);
        if (kept is not null) beat.Record = kept;
        beat.Said.Add(raise
            ? $"The series master's budget is raised to {SeriesTable.Money(beat.Record.BudgetUsd(Dialogue.SeriesMasterBudgetUsd))}."
            : "The series stops: its master's budget is nearly spent and you chose to stop it.");
    }

    /// <summary>Whether the master's budget is spent — the configured one plus what was raised.</summary>
    public bool Spent(SeriesRunRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.MasterCostUsd >= record.BudgetUsd(Dialogue.SeriesMasterBudgetUsd);
    }

    /// <summary>Tells the conversation once that the master judges no more, naming what lets it judge again as things
    /// stand: the waiting budget question, or the setting. Answers the step's line.</summary>
    public async Task<string> SpentAsync(SeriesBeat beat, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(beat);
        var budget = beat.Record.BudgetUsd(Dialogue.SeriesMasterBudgetUsd);
        if (!beat.Record.BudgetSaid && await store.UpdateAsync(beat.Record.Id, r => r with { BudgetSaid = true }, ct) is { } said)
        {
            beat.Record = said;
            beat.Said.Add($"The series master has spent {SeriesTable.Money(said.MasterCostUsd)} of its {SeriesTable.Money(budget)} "
                + "(dialogue.series_master_budget_usd), so it judges no more; the tickets keep running. "
                + (said.Question is { Budget: true, Answer: null }
                    ? "Answer the budget question to raise it or stop the series."
                    : "Raise dialogue.series_master_budget_usd to let it judge again."));
        }
        return $"The series master's budget of {budget} USD (dialogue.series_master_budget_usd) is spent.";
    }

    // The master's own question comes first; a relayed ticket question gives way and is relayed again afterwards.
    private static bool Blocked(SeriesRunRecord record) => record.Question is { Answer: null, TicketId: null };

    private SeriesQuestion Question(SeriesRunRecord record, decimal left, DateTimeOffset now) =>
        new(Guid.NewGuid().ToString("N"),
            $"The series master has spent {SeriesTable.Money(record.MasterCostUsd)} of its "
            + $"{SeriesTable.Money(record.BudgetUsd(Dialogue.SeriesMasterBudgetUsd))} (dialogue.series_master_budget_usd"
            + (record.BudgetRaisedUsd > 0 ? $" plus {SeriesTable.Money(record.BudgetRaisedUsd)} raised here" : string.Empty) + "); "
            + $"{(left > 0 ? SeriesTable.Money(left) : "nothing")} is left, at or below {SeriesTable.Money(Dialogue.SeriesMasterBudgetAskUsd)} "
            + "(dialogue.series_master_budget_ask_usd). Once it is spent the master judges no more. Raise its budget, or stop the series?",
            [$"Raise the budget by {SeriesTable.Money(Dialogue.SeriesMasterBudgetUsd)}", "Stop the series"], 0, now, null)
        { NoDefault = true, Budget = true };
}
