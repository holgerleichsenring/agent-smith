using System.ComponentModel;
using AgentSmith.Contracts.Series;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-25-9992f: the series master changes its series' cut itself (<see cref="ISeriesEditActs"/>): it adds a ticket — a
/// prerequisite, or a part split off a ticket that grew too large — and adds or removes requirement edges between its
/// tickets. Each takes the master's reason, is said with it (<see cref="SeriesActSay"/>) and logged as every act is.
/// 2026-11-27-3532a: an added ticket is filed from a spec, approved under the series' plan.
/// </summary>
public sealed class SeriesEditTools(SeriesBeat beat, ISeriesEditActs edits, ISeriesRunStore store)
{

    public IEnumerable<AIFunction> GetTools() =>
    [
        AIFunctionFactory.Create(AddTicketAsync, SeriesAct.AddTicketTool),
        AIFunctionFactory.Create(LinkTicketsAsync, "link_tickets"),
    ];

    [Description("Files a new ticket into the series from a spec (a spec yaml: spec, goal, steps, done), approved under the series' "
        + "plan, so its run works from that spec — a prerequisite other tickets wait for (name them in requiredBy), or a part split off "
        + "a ticket that grew too large (tell_ticket then narrows the original). It is filed unstarted with its requirement edges: it "
        + "requires the tickets in requires, and each ticket in requiredBy requires it; the spec's own requires are not read. A spec "
        + "that is not schema-valid files nothing and the answer names the schema's error. start_ticket starts it.")]
    public async Task<string> AddTicketAsync(
        [Description("The ticket's spec as yaml, the evidence that calls for it in its goal or facts; its goal titles the ticket.")] string spec,
        [Description("Keys or ids of the series' tickets the new ticket requires.")] string[]? requires = null,
        [Description("Keys or ids of the series' tickets that require the new ticket.")] string[]? requiredBy = null,
        [Description(SeriesActSay.Why)] string? reason = null, CancellationToken ct = default)
    {
        if (Denied() is { } denied) return denied;
        if (!TryIds(requires, out var needs, out var unknown) || !TryIds(requiredBy, out var neededBy, out unknown)) return unknown!;
        var added = await edits.AddTicketAsync(beat.Record, spec, needs, neededBy, ct);
        if (!added.Changed || added.TicketId is not { } id) return added.Answer;
        if (await store.GetAsync(beat.Record.Id, ct) is { } read) beat.Record = read;
        var key = added.Key ?? id;
        var member = beat.Record.Tickets.FirstOrDefault(t => t.TicketId == id) ?? new SeriesTicket(id, key, key, false, beat.Now); // its filed title
        beat.Facts = [.. beat.Facts, new SeriesTicketFact(member) { Requires = needs }];
        beat.ActTicket = id;
        return SeriesActSay.Acted(beat, $"added {key}", reason, added.Answer);
    }

    [Description("Changes what a ticket of the series waits for: it requires each ticket in addRequires from now on, and no longer the ones "
        + "in removeRequires. A pull request stacked on a ticket it no longer requires keeps its target; retarget_pull_request moves it.")]
    public async Task<string> LinkTicketsAsync([Description("The ticket's key or id.")] string ticket,
        [Description("Keys or ids of the series' tickets it requires from now on.")] string[]? addRequires = null,
        [Description("Keys or ids of the series' tickets it no longer requires.")] string[]? removeRequires = null,
        [Description(SeriesActSay.Why)] string? reason = null, CancellationToken ct = default)
    {
        if (SeriesTicketLookup.Find(beat, ticket) is not { } fact) return SeriesTicketLookup.Unknown(beat, ticket);
        if (Denied() is { } denied) return denied;
        if (!TryIds(addRequires, out var add, out var unknown) || !TryIds(removeRequires, out var remove, out unknown)) return unknown!;
        var linked = await edits.LinkAsync(beat.Record, fact.Ticket.TicketId, add, remove, ct);
        return linked.Changed ? SeriesActSay.Acted(beat, $"re-linked {fact.Ticket.Key}", reason, linked.Answer) : linked.Answer;
    }

    private string? Denied() => beat.Record.MayStartRuns ? null
        : $"The series' approver {beat.Record.Principal} may not start runs (runs.control), so the series changes no ticket.";

    // The series' tickets by the names the master used; the first name that is no ticket of the series is answered.
    private bool TryIds(string[]? names, out IReadOnlyList<string> ids, out string? unknown)
    {
        var found = new List<string>();
        (ids, unknown) = (found, null);
        foreach (var name in names ?? [])
        {
            if (SeriesTicketLookup.Find(beat, name) is not { } fact)
            {
                unknown = SeriesTicketLookup.Unknown(beat, name);
                return false;
            }
            found.Add(fact.Ticket.TicketId);
        }
        return true;
    }
}
