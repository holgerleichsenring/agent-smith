using System.ComponentModel;
using AgentSmith.Contracts.Series;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-27-3532d: the series master retires a ticket's spec that cannot be implemented and writes its replacement
/// (<see cref="ISeriesSpecRetirement"/>), for the ticket's next attempt. Said with the master's reason and logged as every
/// act is.
/// </summary>
public sealed class SeriesSpecTools(SeriesBeat beat, ISeriesSpecRetirement retirement)
{
    public const string RetireSpecTool = "retire_spec";

    public IEnumerable<AIFunction> GetTools() => [AIFunctionFactory.Create(RetireSpecAsync, RetireSpecTool)];

    [Description("Retires a spec of a ticket that has not run and cannot be implemented as written, and writes its replacement: the "
        + "retired spec stays in the ticket's series as written, marked superseded, and the replacement gets the series' next id, "
        + "takes its place and is what later specs require. Recorded for the ticket's next attempt; nothing starts. It works on a "
        + "ticket an attempt has worked, in a series led from a conversation. A run of the ticket parked on a question answers "
        + "first: its coding master can retire the spec itself with revise_spec, so tell it in your answer.")]
    public async Task<string> RetireSpecAsync([Description("The ticket's key or id as the table shows it.")] string ticket,
        [Description("The id of the spec to retire.")] string spec,
        [Description("The replacement's whole spec yaml; its spec id is minted anew.")] string yaml,
        [Description(SeriesActSay.Why)] string? reason = null, CancellationToken ct = default)
    {
        if (SeriesTicketLookup.Find(beat, ticket) is not { } fact) return SeriesTicketLookup.Unknown(beat, ticket);
        if (!beat.Record.MayStartRuns)
            return $"The series' approver {beat.Record.Principal} may not start runs (runs.control), so the series changes no spec.";
        var retired = await retirement.RetireAsync(beat.Record, fact.Ticket.TicketId, spec, yaml, ct);
        beat.ActTicket = fact.Ticket.TicketId;
        return retired.Changed ? SeriesActSay.Acted(beat, $"retired {spec} of {fact.Ticket.Key}", reason, retired.Answer) : retired.Answer;
    }
}
