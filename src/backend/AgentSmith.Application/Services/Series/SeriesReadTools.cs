using System.ComponentModel;
using System.Text.Json;
using AgentSmith.Contracts.Series;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: the series master's reads over the beat's facts — the table, and one ticket's facts.</summary>
public sealed class SeriesReadTools(SeriesBeat beat)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public IEnumerable<AIFunction> GetTools() =>
    [
        AIFunctionFactory.Create(SeriesStatus, "series_status"),
        AIFunctionFactory.Create(ReadTicket, "read_ticket"),
    ];

    [Description("The series' tickets as a table: ticket, what, state, cost, time, the tickets each requires, and the series' state and spend.")]
    public string SeriesStatus() => JsonSerializer.Serialize(new
    {
        series = beat.Record.Id, state = beat.Record.State, masterCostUsd = beat.Record.MasterCostUsd,
        tickets = beat.Rows.Zip(beat.Facts, (r, f) => new { r.Ticket, r.What, r.State, r.Cost, r.Time, requires = Keys(f.Requires) }),
    }, Json);

    [Description("One ticket's facts: its newest run, that run's status and why it ended, its pull requests, its cost, when it started and landed, and a question its run is parked on.")]
    public string ReadTicket([Description("The ticket's key or id as the table shows it.")] string ticket) =>
        SeriesTicketLookup.Find(beat, ticket) is { } fact
            ? JsonSerializer.Serialize(new
            {
                ticket = fact.Ticket.Key, fact.Ticket.TicketId, fact.Ticket.Title, fact.LatestRunId, fact.LatestStatus,
                fact.LatestFinished, fact.EndDetail, fact.PullRequests, fact.CostUsd, fact.FirstStartedAt, fact.LandedAt,
                requires = Keys(fact.Requires), question = fact.PendingQuestion?.Text,
            }, Json)
            : SeriesTicketLookup.Unknown(beat, ticket);

    private IReadOnlyList<string> Keys(IReadOnlyList<string> ids) =>
        [.. ids.Select(id => beat.Facts.FirstOrDefault(f => f.Ticket.TicketId == id)?.Ticket.Key ?? id)];
}
