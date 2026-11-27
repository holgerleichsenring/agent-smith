using System.ComponentModel;
using AgentSmith.Contracts.Series;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: what the series master does — through the paths a person's act takes, as the series' approver.
/// Start, retry and tell reach a member ticket; stop ends the series and hands the conversation back to the design partner.
/// 2026-11-25-9992f: the master re-cuts the series itself (<see cref="SeriesEditTools"/>), so no re-cut is proposed any more. 2026-11-18-3863h: deploy, to a no-approval environment.
/// 2026-11-19-1b72a: pause and resume, the act the conversation head's buttons take; a paused series starts no ticket.
/// 2026-11-25-9992h: each act is said in the conversation under the machine identity the series acts as, with the reason the
/// master gives for it; the approver's runs.control stays the authority.
/// </summary>
public sealed class SeriesActTools(SeriesBeat beat, ISeriesTicketActs acts, ISeriesRunStore store, SeriesPause pause)
{
    public IEnumerable<AIFunction> GetTools() =>
    [
        AIFunctionFactory.Create(StartTicketAsync, "start_ticket"),
        AIFunctionFactory.Create(RetryTicketAsync, "retry_ticket"),
        AIFunctionFactory.Create(TellTicketAsync, "tell_ticket"),
        AIFunctionFactory.Create(StopSeriesAsync, "stop_series"),
        AIFunctionFactory.Create(DeployAsync, "deploy"), // 2026-11-18-3863h
        AIFunctionFactory.Create(PauseSeriesAsync, "pause_series"), // 2026-11-19-1b72a
        AIFunctionFactory.Create(ResumeSeriesAsync, "resume_series"),
    ];

    [Description("Pauses the series when the operator asks to hold it: no ticket of it starts until it is resumed, and a running run finishes. "
        + "With stopNow the running runs are cancelled too — for 'stop now', not for 'hold on'. The conversation is told in one line.")]
    public async Task<string> PauseSeriesAsync(
        [Description("True to cancel the runs under way as well.")] bool stopNow = false,
        [Description(SeriesActSay.Why)] string? reason = null, CancellationToken ct = default)
    {
        var said = await pause.PauseAsync(beat.Record.Id, stopNow, ct);
        await RereadAsync(ct);
        return SeriesActSay.Acted(beat, "paused the series", reason, said);
    }

    [Description("Resumes a paused series when the operator says to go on: it starts every ticket that is ready and runs on as before. The conversation is told in one line.")]
    public async Task<string> ResumeSeriesAsync([Description(SeriesActSay.Why)] string? reason = null, CancellationToken ct = default)
    {
        var said = await pause.ResumeAsync(beat.Record.Id, ct);
        await RereadAsync(ct);
        return SeriesActSay.Acted(beat, "resumed the series", reason, said);
    }

    [Description("Starts a deploy of a context to an environment its deployment binding delivers without a person's approval, as a person's Deploy does; an approval environment is refused. Name the version for an entry environment; leave it empty for one that promotes from another.")]
    public async Task<string> DeployAsync(
        [Description("The context of the project to deliver.")] string context, [Description("The environment.")] string environment,
        [Description("The version (a tag or commit); empty to promote.")] string? version = null,
        [Description("The member ticket whose page lists the deploy, if any.")] string? ticket = null,
        [Description(SeriesActSay.Why)] string? reason = null, CancellationToken ct = default)
    {
        if (!beat.Record.MayStartRuns)
            return $"The series' approver {beat.Record.Principal} may not start runs (runs.control), so the series deploys nothing.";
        var listed = ticket is null ? null : SeriesTicketLookup.Find(beat, ticket)?.Ticket.TicketId;
        return SeriesActSay.Acted(beat, $"deployed {context} to {environment}", reason,
            await acts.DeployAsync(beat.Record.Project, context, environment, string.IsNullOrWhiteSpace(version) ? null : version, listed, ct));
    }

    [Description("Starts a ticket no run has worked yet, once the tickets it requires are met. The answer says whether a run started "
        + "(its run id), was queued (and why), or was refused (why, and what lifts it).")]
    public async Task<string> StartTicketAsync([Description("The ticket's key or id.")] string ticket,
        [Description(SeriesActSay.Why)] string? reason = null, CancellationToken ct = default)
    {
        if (SeriesTicketLookup.Find(beat, ticket) is not { } fact) return SeriesTicketLookup.Unknown(beat, ticket);
        if (!beat.Record.MayStartRuns)
            return $"The series' approver {beat.Record.Principal} may not start runs (runs.control), so the series acts on no ticket.";
        // 2026-11-26-5f6e: the act is logged and said as what the start came to, never as 'started' when nothing runs
        var start = Paused is { } paused ? SeriesStart.Refused(await paused)
            : await acts.StartAsync(beat.Record.Project, fact.Ticket.TicketId, ct);
        return SeriesActSay.Acted(beat, SeriesActSay.Started(fact.Ticket.Key, start), reason, start.Text);
    }

    [Description("Retries a ticket's newest code run unchanged, after any end but a success — for a failure that was not the work's fault.")]
    public Task<string> RetryTicketAsync([Description("The ticket's key or id.")] string ticket,
        [Description(SeriesActSay.Why)] string? reason = null, CancellationToken ct = default) =>
        OnTicketAsync(ticket, "retried", reason, fact => acts.RetryAsync(beat.Record.Project, fact.Ticket.TicketId, beat.Record.Principal, ct));

    [Description("Answers a ticket whose run is parked on a question with the text: the run resumes with it; with questionId only that question, and a run that waits on another is named instead. Otherwise writes into the ticket's conversation as the series: a running attempt reads it with its next tool result; otherwise the ticket's steward answers and may revise the ticket's spec or rework it. Use it to pass on what the operator said or what a failure showed.")]
    public Task<string> TellTicketAsync(
        [Description("The ticket's key or id.")] string ticket, [Description("What the ticket's agent should know or do.")] string text,
        [Description(SeriesActSay.Why)] string? reason = null,
        [Description("The id of the run's question this answers, as its reason named it.")] string? questionId = null, CancellationToken ct = default) =>
        OnTicketAsync(ticket, "wrote to", reason, fact => acts.TellAsync(new SeriesTell(beat.Record.Project, fact.Ticket.TicketId,
            fact.Ticket.Key, text, beat.Record.Principal, beat.Record.MayStartRuns, questionId), ct));

    [Description("Ends the series: its tickets keep running on their own, the status reports stop, and the conversation goes back to the design partner. "
        + "To hold its tickets instead — 'halt', 'stop', 'wait' — pause_series.")]
    public async Task<string> StopSeriesAsync([Description("Why the series stops, said in the conversation.")] string reason, CancellationToken ct = default)
    {
        var stopped = await store.UpdateAsync(beat.Record.Id, r => r with { State = SeriesRunStates.Stopped, FinishedAt = beat.Now }, ct);
        if (stopped is not null) beat.Record = stopped;
        beat.Said.Add($"The series stops: {reason}");
        return "The series is stopped.";
    }

    private async Task<string> OnTicketAsync(string ticket, string did, string? reason, Func<SeriesTicketFact, Task<string>> act)
    {
        if (SeriesTicketLookup.Find(beat, ticket) is not { } fact) return SeriesTicketLookup.Unknown(beat, ticket);
        if (!beat.Record.MayStartRuns)
            return $"The series' approver {beat.Record.Principal} may not start runs (runs.control), so the series acts on no ticket.";
        return SeriesActSay.Acted(beat, $"{did} {fact.Ticket.Key}", reason, await act(fact));
    }


    // 2026-11-19-1b72a: a paused series starts no new ticket, by its own hand neither; a retry is no new ticket.
    private Task<string>? Paused => beat.Record.State == SeriesRunStates.Paused
        ? Task.FromResult("The series is paused, so it starts no new ticket; resume_series first when the operator says to go on.")
        : null;

    private async Task RereadAsync(CancellationToken ct)
    {
        if (await store.GetAsync(beat.Record.Id, ct) is { } read) beat.Record = read;
    }
}
