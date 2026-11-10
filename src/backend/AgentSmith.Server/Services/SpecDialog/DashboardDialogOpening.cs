namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-20-4b0aa: a dashboard message that names a project opens its own conversation first. Carved out of
/// <see cref="DashboardDialogDispatcher"/> by 2026-11-10-ccd9, whose message now also carries the card it was
/// sent from.
/// </summary>
public sealed class DashboardDialogOpening(
    SpecDialogConversationResolver conversations,
    TicketConversationBinder binder,
    TicketTextForConversation ticketText,
    AgentSmith.Contracts.Services.IConfigurationLoader configLoader,
    AgentSmith.Contracts.Models.Configuration.ServerContext serverContext)
{
    /// <summary>
    /// Whether routing should go ahead. With no project named there is nothing to open on, so the
    /// message takes its chances with the router exactly as it did before — an open session
    /// continues, and a dialog with none gets the tutorial, which is the caller that really has
    /// nothing to open. With a project named, the conversation is resolved or opened FIRST.
    /// <para>
    /// A resolve that yields no session has already been answered: the command handler names the
    /// unknown project or the choice to be made, and a foreign conversation was refused by the
    /// route before this task started. Routing on would only add the tutorial on top of a reason
    /// that is already truer than it.
    /// </para>
    /// </summary>
    public async Task<bool> OpenedAsync(
        string dialogId, string? project, string userId, CancellationToken cancellationToken,
        string? ticketId)
    {
        if (string.IsNullOrWhiteSpace(project)) return true;
        // Only a conversation that NAMES a ticket reads the catalog here; one without must reach
        // the resolver exactly as it did before, config read included.
        var resolved = string.IsNullOrWhiteSpace(ticketId) ? null : Resolved(project!);
        var binding = resolved is null
            ? null
            : await binder.BindingForAsync(resolved, ticketId!, cancellationToken);
        var target = await conversations.ResolveOrOpenAsync(
            dialogId, project, userId, cancellationToken, binding);
        if (target.SessionId is null) return false;
        // 2026-09-25-8e51c: the ticket is read ONCE, here, where the conversation has just come
        // into existence — a turn reads what was kept, not the tracker. 481be: and says it is.
        if (binding is not null && resolved is not null)
            await ticketText.ReadAsync(target.SessionId, resolved, binding.TicketId, dialogId, cancellationToken);
        return true;
    }

    /// <summary>The project this conversation is on, or null when the catalog does not hold it.</summary>
    private AgentSmith.Contracts.Models.Configuration.ResolvedProject? Resolved(string project) =>
        configLoader.LoadConfig(serverContext.ConfigPath)?.Projects
            .TryGetValue(project, out var resolved) == true ? resolved : null;
}
