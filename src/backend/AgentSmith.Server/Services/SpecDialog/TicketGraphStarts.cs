using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>One created ticket of a list: its stored requires edges, and whether its filing is COMPLETE — created, its
/// edges stored and its ticket-text approval record stored last.</summary>
public sealed record CreatedGraphTicket(CreatedTicket Ticket, IReadOnlyList<TicketRequirementEdge> Edges, bool Complete);

/// <summary>
/// 2026-10-14-dbcel: what each created ticket of a filed list became, decided ONCE over the created prefix — after
/// the last ticket and after a failure alike, never on the caller's token, because a root left unstarted is never
/// started by anything else. A ticket whose filing is not complete is never tagged or moved; a complete one with
/// nothing unmet is started as any filed ticket is; any other waits, naming what it waits for by tracker key.
/// 2026-10-14-dbcem: then every stored edge of the prefix is shown on the tracker, after the starts so a slow tracker
/// never delays one; what it refused or never reached comes back as filing notes.
/// </summary>
public sealed class TicketGraphStarts(
    UnmetTicketRequirements requirements,
    FiledWorkStarter starter,
    TicketGraphLinks links,
    ILogger<TicketGraphStarts> logger)
{
    internal const string Incomplete = "created but not stored — do not trigger it; close it";

    public async Task<IReadOnlyList<string>> SettleAsync(
        ITicketProvider provider, ResolvedProject project, IReadOnlyList<CreatedGraphTicket> created,
        IReadOnlyList<string> labels, bool mayStartRuns, List<FiledTicket> filed)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(created);
        foreach (var ticket in created)
        {
            if (!ticket.Complete)
            {
                Stamp(filed, ticket.Ticket, new FiledWorkStart(FiledStartState.NotStarted, Incomplete));
                continue;
            }
            var unmet = await UnmetAsync(project, ticket.Ticket);
            if (unmet is { Count: 0 })
                await starter.StampAsync(provider, project, ticket.Ticket, labels, mayStartRuns, filed, CancellationToken.None);
            else
                Stamp(filed, ticket.Ticket, unmet is null
                    ? new FiledWorkStart(FiledStartState.NotStarted,
                        "what it requires could not be read — start it by name once they are merged or marked done by hand")
                    : new FiledWorkStart(FiledStartState.Held, $"waits for {string.Join(", ", unmet.Select(e =>
                        FiledTicketKey.Of(project.Tracker.Type, e.RequiredTicketId)))} to be merged or marked done by hand"));
        }
        return await links.LinkAsync(project, [.. created.SelectMany(t => t.Edges)]);
    }

    // Null when the read failed: a start pass that throws would cost the report of every ticket already created.
    private async Task<IReadOnlyList<TicketRequirementEdge>?> UnmetAsync(ResolvedProject project, CreatedTicket ticket)
    {
        try
        {
            var key = ApprovedSpecSetRecorder.KeyFor(project, ticket.Id.Value).Value;
            return (await requirements.OfAsync(project.Tracker.Name, key, CancellationToken.None)).Unmet;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The requirements of filed ticket {Ticket} could not be read", ticket.Id.Value);
            return null;
        }
    }

    private void Stamp(List<FiledTicket> filed, CreatedTicket ticket, FiledWorkStart start)
    {
        logger.LogInformation("Filed ticket {Ticket} is {State}: {Reason}", ticket.Id.Value, start.State, start.Reason);
        var at = filed.FindIndex(t => t.TicketId == ticket.Id.Value);
        if (at >= 0) filed[at] = filed[at] with { Start = start };
    }
}
