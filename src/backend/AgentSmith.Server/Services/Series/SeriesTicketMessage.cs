using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-17-0207a: a message from the series into a member ticket's conversation, routed exactly as the operator's
/// would be — a running attempt reads it with its next tool result, otherwise the ticket's steward takes the turn. The
/// route runs in its own scope after the answer, because a steward's turn takes longer than the beat should wait.
/// </summary>
public sealed class SeriesTicketMessage(
    TicketProjects projects, TicketConversationBinder binder, IServiceScopeFactory scopes, ILogger<SeriesTicketMessage> logger)
{
    public const string Author = "series master";

    public async Task<string> SendAsync(
        string project, string ticketId, string text, string principal, bool mayStartRuns, CancellationToken ct)
    {
        if (projects.Of(project) is not { } resolved) return $"Project '{project}' is not configured.";
        if (await binder.BindingForAsync(resolved, ticketId, ct) is not { } binding)
            return $"Ticket {ticketId} could not be read from its tracker; nothing was sent.";
        var dialogId = (await binder.ExistingAsync(binding, ct))?.OpenDialogId ?? Guid.NewGuid().ToString("N");
        _ = Task.Run(() => RouteAsync(dialogId, text, principal, mayStartRuns, project, ticketId), CancellationToken.None);
        return $"Sent to ticket {ticketId}'s conversation: its running attempt reads it with its next tool result, "
            + "or its steward answers there.";
    }

    private async Task RouteAsync(string dialogId, string text, string principal, bool mayStartRuns, string project, string ticketId)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DashboardDialogDispatcher>().DispatchAsync(
                dialogId, text, principal, mayStartRuns, project, CancellationToken.None, ticketId, Author);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The series' message to ticket {Ticket} of {Project} could not be routed", ticketId, project);
        }
    }
}
