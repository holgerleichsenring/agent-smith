using System.Text.Json;
using AgentSmith.Contracts.Series;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-25-e94ec: wraps one of the series master's acts — a ticket act, a pause, a question, the series check — so
/// the beat knows the master did something a person reads about. The beat posts the master's reply only then, when
/// its own question was answered, or when the operator wrote; a beat in which it only read says nothing. An attempt
/// counts, refused or not: "I could not retry it" is worth the operator's reading.
/// <para>2026-11-25-9992e: and logs it — a <see cref="SeriesAct"/> with the call's ticket (its id), what it did and its reason
/// goes on the series' record (<see cref="SeriesRunRecord.Acts"/>) and into the log.</para>
/// </summary>
public sealed class SeriesActedFunction(AIFunction inner, SeriesBeat beat, ISeriesRunStore store, ILogger logger, TimeProvider? clock = null)
    : AIFunction
{
    public override string Name => inner.Name;
    public override string Description => inner.Description;
    public override JsonElement JsonSchema => inner.JsonSchema;
    public override JsonSerializerOptions JsonSerializerOptions => inner.JsonSerializerOptions;

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        // A CI read changes nothing a person reads about; it is logged, but the beat stays a looking one.
        beat.MasterActed |= !(Name == SeriesPullRequestTools.CiTool
            && string.Equals(Text(arguments, "act")?.Trim(), SeriesCiActs.Read, StringComparison.OrdinalIgnoreCase));
        (beat.ActDid, beat.ActTicket) = (null, null);
        var started = (clock ?? TimeProvider.System).GetUtcNow(); // 2026-11-25-96aec: what it recorded on its ticket is its own
        var result = await inner.InvokeAsync(arguments, cancellationToken);
        var ticket = beat.ActTicket ?? TicketOf(Text(arguments, "ticket")); // 2026-11-25-9992f: an added ticket by its new id
        var act = new SeriesAct(beat.Now, Name, ticket, beat.ActDid ?? Name, Text(arguments, "reason"))
        {
            StartedAt = started, DoneAt = (clock ?? TimeProvider.System).GetUtcNow(),
        };
        if (await store.UpdateAsync(beat.Record.Id, r => r with { Acts = [.. r.Acts, act] }, cancellationToken) is { } kept)
            beat.Record = kept;
        logger.LogInformation("Series {Series}: the master acted — {Tool} on {Ticket}, because {Reason}: {Outcome}",
            beat.Record.Id, act.Tool, act.Ticket ?? "the series", act.Reason ?? "(no reason given)", Outcome(result));
        return result;
    }

    // The ticket by its id when it is a member, so a ticket's last act is found whichever name the master used.
    private string? TicketOf(string? named) => named is null ? null : SeriesTicketLookup.Find(beat, named)?.Ticket.TicketId ?? named;

    private static string? Text(AIFunctionArguments arguments, string name) =>
        arguments.TryGetValue(name, out var value) ? Outcome(value) : null;

    // A tool's answer is a string, or the JSON the function factory marshalled it to.
    private static string? Outcome(object? value) => value switch
    {
        null => null,
        string text => string.IsNullOrWhiteSpace(text) ? null : text,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
        JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
        _ => value.ToString(),
    };
}
