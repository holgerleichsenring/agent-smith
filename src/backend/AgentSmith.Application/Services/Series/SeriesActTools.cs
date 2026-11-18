using System.ComponentModel;
using AgentSmith.Contracts.Series;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: what the series master does — through the paths a person's act takes, as the series' approver.
/// Start, retry and tell reach a member ticket; a re-cut is proposed to the operator in the conversation; stop ends the
/// series and hands the conversation back to the design partner.
/// </summary>
public sealed class SeriesActTools(SeriesBeat beat, ISeriesTicketActs acts, ISeriesRunStore store)
{
    public IEnumerable<AIFunction> GetTools() =>
    [
        AIFunctionFactory.Create(StartTicketAsync, "start_ticket"),
        AIFunctionFactory.Create(RetryTicketAsync, "retry_ticket"),
        AIFunctionFactory.Create(TellTicketAsync, "tell_ticket"),
        AIFunctionFactory.Create(ProposeRecut, "propose_recut"),
        AIFunctionFactory.Create(StopSeriesAsync, "stop_series"),
    ];

    [Description("Starts a ticket no run has worked yet, once the tickets it requires are met.")]
    public Task<string> StartTicketAsync([Description("The ticket's key or id.")] string ticket, CancellationToken ct = default) =>
        OnTicketAsync(ticket, fact => acts.StartAsync(beat.Record.Project, fact.Ticket.TicketId, ct));

    [Description("Retries a ticket's newest code run unchanged, after any end but a success — for a failure that was not the work's fault.")]
    public Task<string> RetryTicketAsync([Description("The ticket's key or id.")] string ticket, CancellationToken ct = default) =>
        OnTicketAsync(ticket, fact => acts.RetryAsync(beat.Record.Project, fact.Ticket.TicketId, beat.Record.Principal, ct));

    [Description("Writes into a ticket's conversation as the series: a running attempt reads it with its next tool result; otherwise the ticket's steward answers and may revise the ticket's spec or rework it. Use it to pass on what the operator said or what a failure showed.")]
    public Task<string> TellTicketAsync(
        [Description("The ticket's key or id.")] string ticket, [Description("What the ticket's agent should know or do.")] string text,
        CancellationToken ct = default) =>
        OnTicketAsync(ticket, fact => acts.TellAsync(
            beat.Record.Project, fact.Ticket.TicketId, text, beat.Record.Principal, beat.Record.MayStartRuns, ct));

    [Description("Proposes to the operator, in the conversation, cutting a slow or stuck ticket differently. It changes nothing; the operator answers in the conversation.")]
    public string ProposeRecut([Description("The ticket's key or id.")] string ticket, [Description("The cut you propose and why.")] string proposal)
    {
        if (SeriesTicketLookup.Find(beat, ticket) is not { } fact) return SeriesTicketLookup.Unknown(beat, ticket);
        beat.Said.Add($"Proposal for {fact.Ticket.Key}: {proposal}");
        return "The proposal is posted to the operator with this beat's report.";
    }

    [Description("Ends the series: its tickets keep running on their own, the status reports stop, and the conversation goes back to the design partner.")]
    public async Task<string> StopSeriesAsync([Description("Why the series stops, said in the conversation.")] string reason, CancellationToken ct = default)
    {
        var stopped = await store.UpdateAsync(beat.Record.Id, r => r with { State = SeriesRunStates.Stopped, FinishedAt = beat.Now }, ct);
        if (stopped is not null) beat.Record = stopped;
        beat.Said.Add($"The series stops: {reason}");
        return "The series is stopped.";
    }

    private async Task<string> OnTicketAsync(string ticket, Func<SeriesTicketFact, Task<string>> act)
    {
        if (SeriesTicketLookup.Find(beat, ticket) is not { } fact) return SeriesTicketLookup.Unknown(beat, ticket);
        if (!beat.Record.MayStartRuns)
            return $"The series' approver {beat.Record.Principal} may not start runs (runs.control), so the series acts on no ticket.";
        return await act(fact);
    }
}
