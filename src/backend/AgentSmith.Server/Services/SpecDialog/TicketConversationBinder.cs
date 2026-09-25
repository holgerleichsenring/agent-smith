using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-25-8e51b: turns "discuss ticket X of project P" into the binding a conversation is
/// opened with, and answers which conversation that ticket already has.
/// <para>
/// The ticket is FETCHED once here, for two reasons that are both the point: it is where the
/// heading comes from, and a ticket the tracker does not have must be refused before a
/// conversation exists about it — an unbindable conversation is worse than none.
/// </para>
/// <para>
/// Its own class because the session manager sits seven lines under the class limit, and because
/// this reaches a tracker while the manager reaches only the store.
/// </para>
/// </summary>
public sealed class TicketConversationBinder(
    ITicketProviderFactory providers,
    SpecDialogSessionRepository sessions,
    ILogger<TicketConversationBinder> logger)
{
    /// <summary>
    /// The binding for this ticket, or null when the tracker does not have it.
    /// <para>
    /// NOT called BindAsync: a minimal-API endpoint takes this type as a parameter, and ASP.NET
    /// reads a BindAsync method on a parameter type as a custom model binder — the route then
    /// fails at startup with a signature complaint about a method that was never meant for it.
    /// </para>
    /// </summary>
    public async Task<TicketBinding?> BindingForAsync(
        ResolvedProject project, string ticketId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(ticketId)) return null;
        try
        {
            var ticket = await providers.Create(project.Tracker)
                .GetTicketAsync(new TicketId(ticketId.Trim()), ct);
            return TicketBinding.For(
                project.Tracker.Name, project.Tracker.Type.ToString().ToLowerInvariant(),
                ticket.Id.Value, ticket.Title);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex,
                "Ticket {Ticket} could not be read from tracker {Tracker}; no conversation is bound to it",
                ticketId, project.Tracker.Name);
            return null;
        }
    }

    /// <summary>
    /// The conversation this ticket already has — open or closed — so a second request reaches it
    /// rather than opening a second one the unique index would refuse anyway.
    /// </summary>
    public async Task<TicketConversation?> ExistingAsync(
        TicketBinding binding, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        var session = await sessions.GetByTicketAsync(binding.Tracker, binding.Key, ct);
        return session is null
            ? null
            // The dialog id is only useful while the conversation is OPEN: it is what sends a page
            // to a running conversation instead of queueing a resume the server refuses mid-turn.
            : new TicketConversation(session.SessionId, session.IsOpen ? session.ThreadId : null);
    }
}

/// <summary>Which conversation a ticket has, and the dialog it is living on if it is open.</summary>
public sealed record TicketConversation(string SessionId, string? OpenDialogId);
