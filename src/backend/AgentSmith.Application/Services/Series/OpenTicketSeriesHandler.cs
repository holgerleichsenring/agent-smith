using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-25-96aea: a code run's step right after FetchTicket — a ticket no active series holds gets a series of one
/// (<see cref="SeriesOfOne"/>), so its master leads its pull requests, CI and merge like every series ticket. A run without
/// a tracker ticket (an inline one) and a process without a series store (the CLI) open none. Never fails the run.
/// </summary>
public sealed class OpenTicketSeriesHandler(SeriesOfOne? series = null) : ICommandHandler<OpenTicketSeriesContext>
{
    public async Task<CommandResult> ExecuteAsync(OpenTicketSeriesContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (series is null) return CommandResult.Ok("No series store here: no series of one opened.");
        if (!context.Pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket) || ticket is null
            || context.Pipeline.Has(ContextKeys.InlineTicket))
            return CommandResult.Ok("No tracker ticket: no series of one opened.");
        string? opened;
        try { opened = await series.OpenAsync(context.Project, ticket.Id.Value, ticket.Title, cancellationToken); }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return CommandResult.Ok($"No series of one opened for ticket {ticket.Id.Value}: {ex.Message}");
        }
        return CommandResult.Ok(opened is null ? $"Ticket {ticket.Id.Value} is held by a series already."
            : $"Opened series of one {opened} over ticket {ticket.Id.Value}: its series master leads it.");
    }
}
