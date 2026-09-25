using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-09-25-8e51b: which design conversation a ticket has, and the dialog it is living on.
/// <para>
/// A page cannot work this out for itself. It opens a conversation by SESSION id, and it needs
/// the DIALOG id a running one is already on — that is the only thing that sends it to a live
/// conversation instead of queueing a resume the server refuses mid-turn. Nothing maps a ticket
/// to either, and the conversation list carries no ticket.
/// </para>
/// <para>
/// It answers for a ticket, which every other read on this surface refuses to do — and that is
/// safe here for a reason worth writing down: it returns an IDENTIFIER and no content. Reading
/// the conversation still goes through the per-dialog route, whose reach check decides whether
/// this caller may see it at all.
/// </para>
/// </summary>
internal static class TicketConversationEndpoints
{
    internal static WebApplication MapTicketConversationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/spec-dialog/tickets/{project}/{ticketId}", (Delegate)ForTicketAsync)
           .Needs(Security.Permissions.DialogWrite);
        // 2026-09-25-8e51a: the ticket alone. Every configured tracker is asked for it, and the
        // projects routed to the one that has it are matched from its own labels.
        app.MapGet("/api/spec-dialog/tickets/{ticketId}", (Delegate)ProjectForTicketAsync)
           .Needs(Security.Permissions.DialogWrite);
        return app;
    }

    internal static async Task<IResult> ProjectForTicketAsync(
        string ticketId,
        IConfigurationLoader configLoader,
        ServerContext serverContext,
        TicketProjectChoice choice,
        TicketConversationBinder binder,
        CancellationToken cancellationToken)
    {
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        var answer = await choice.ForAsync(config, ticketId, cancellationToken);
        if (answer is null)
            return Results.NotFound(
                new { reason = $"No configured tracker has a ticket '{ticketId}'." });

        var existing = await binder.ExistingAsync(answer.Binding, cancellationToken);
        return Results.Ok(new
        {
            ticketId = answer.Binding.TicketId,
            title = answer.Binding.Title,
            tracker = answer.Binding.Tracker,
            projects = answer.Projects,
            unanswerable = answer.Unanswerable,
            sessionId = existing?.SessionId,
            openDialogId = existing?.OpenDialogId,
        });
    }

    internal static async Task<IResult> ForTicketAsync(
        string project,
        string ticketId,
        IConfigurationLoader configLoader,
        ServerContext serverContext,
        TicketConversationBinder binder,
        CancellationToken cancellationToken)
    {
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        if (!config.Projects.TryGetValue(project, out var resolved))
            return Results.NotFound(new { reason = $"No project named '{project}'." });

        var binding = await binder.BindingForAsync(resolved, ticketId, cancellationToken);
        if (binding is null)
            return Results.NotFound(
                new { reason = $"Ticket '{ticketId}' could not be read from the project's tracker." });

        var existing = await binder.ExistingAsync(binding, cancellationToken);
        return Results.Ok(new
        {
            ticketId = binding.TicketId,
            title = binding.Title,
            sessionId = existing?.SessionId,
            openDialogId = existing?.OpenDialogId,
        });
    }
}
