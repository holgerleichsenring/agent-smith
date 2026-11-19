using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-19-1b72a: pausing and resuming a series — the one act behind the master's pause_series and resume_series and
/// the conversation head's Pause and Resume. A pause holds every new ticket of the series (the claim gate reads the
/// state) and the automatic CI fix (the CI act reads it), while a run under way finishes, or is cancelled too when asked
/// to stop now. A resume returns the series to where it was, starts what is ready exactly as the wake would, and hands
/// red CIs back to the CI loop. Each says so in the conversation in one line (<see cref="SeriesPauseText"/>).
/// </summary>
public sealed class SeriesPause(
    ISeriesRunStore store, ISeriesTicketFacts facts, ISeriesTicketActs acts, ISeriesConversation conversation)
{
    public async Task<string> PauseAsync(string seriesId, bool cancelRunning, CancellationToken ct)
    {
        if (await store.GetAsync(seriesId, ct) is not { } record) return $"Series run {seriesId} does not exist.";
        if (!record.IsActive) return $"The series is {record.State}; there is nothing to pause.";
        var already = record.State == SeriesRunStates.Paused;
        if (already && !cancelRunning) return "The series is already paused.";
        if (!already)
            await store.UpdateAsync(seriesId, r => r.State == SeriesRunStates.Paused ? r
                : r with { State = SeriesRunStates.Paused, PausedFrom = r.State }, ct);
        var all = await facts.ReadAsync(record.Project, record.Tickets, ct);
        var line = SeriesPauseText.Pause(already, all, await RunsAsync(record, [.. all.Where(SeriesPauseText.Running)], cancelRunning, ct));
        await conversation.SayAsync(record.Conversation, line, ct);
        return line;
    }

    public async Task<string> ResumeAsync(string seriesId, CancellationToken ct)
    {
        if (await store.GetAsync(seriesId, ct) is not { } record) return $"Series run {seriesId} does not exist.";
        if (record.State != SeriesRunStates.Paused) return $"The series is {record.State}, not paused.";
        await store.UpdateAsync(seriesId, r => r.State != SeriesRunStates.Paused ? r
            : r with { State = r.PausedFrom ?? SeriesRunStates.Running, PausedFrom = null }, ct);
        var all = await facts.ReadAsync(record.Project, record.Tickets, ct);
        var (started, answers) = await StartReadyAsync(record, all, ct);
        foreach (var fact in all.Where(SeriesPauseText.CiRed)) answers.Add(await acts.AskCiLoopAsync(record.Project, fact.Ticket.TicketId, ct));
        var line = SeriesPauseText.Resume(started, all);
        await conversation.SayAsync(record.Conversation, line, ct);
        return string.Join(" ", [line, .. answers]);
    }

    // Every never-worked (or only queued), unlanded ticket is offered to the start, which holds what the wake would hold —
    // a closed or hand-marked ticket, unmet requirements, a start by name, another paused series.
    private async Task<(List<SeriesTicketFact> Started, List<string> Answers)> StartReadyAsync(
        SeriesRunRecord record, IReadOnlyList<SeriesTicketFact> all, CancellationToken ct)
    {
        var (started, answers) = (new List<SeriesTicketFact>(), new List<string>());
        if (!record.MayStartRuns) return (started, [$"The series' approver {record.Principal} may not start runs (runs.control), so nothing starts."]);
        foreach (var fact in all.Where(f => !f.Landed && f.LatestStatus is null or RunStatuses.Queued))
        {
            var start = await acts.StartIfReadyAsync(record.Project, fact.Ticket.TicketId, ct);
            answers.Add(start.Text);
            if (start.Started) started.Add(fact);
        }
        return (started, answers);
    }

    private async Task<string> RunsAsync(SeriesRunRecord record, List<SeriesTicketFact> running, bool cancel, CancellationToken ct)
    {
        if (running.Count == 0) return string.Empty;
        var keys = SeriesPauseText.Keys(running);
        if (!cancel) return $"{keys} finish{(running.Count == 1 ? "es" : string.Empty)} the run under way. ";
        if (!record.MayStartRuns) return $"{keys} keep running: the series' approver {record.Principal} may not cancel runs (runs.control). ";
        foreach (var fact in running) await acts.CancelAsync(fact.LatestRunId!, ct);
        return $"The run{(running.Count == 1 ? string.Empty : "s")} of {keys} {(running.Count == 1 ? "is" : "are")} cancelled. ";
    }
}
