using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Tickets.Conversation;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-13-9a59h: a ticket's conversation, runs.read like the rest of the ticket surface. The bound Work
/// it out turns and the open proposal are dialog.write content, so they are checked in the handler the way
/// MayStartRuns checks runs.control: a non-enforcing installation shows them to everyone.
/// </summary>
internal static class TicketConversationReadEndpoints
{
    internal static WebApplication MapTicketConversationReadEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tickets/{project}/{ticketId}/conversation", ConversationAsync).Needs(Permissions.RunsRead);
        return app.MapTicketTextEndpoints();
    }

    // Internal so the endpoint tests drive the real reader without a host.
    internal static async Task<IResult> ConversationAsync(
        string project, string ticketId, HttpContext ctx, TicketConversationReader reader, CancellationToken cancellationToken)
    {
        var view = await reader.ReadAsync(TicketQueryEndpoints.Decoded(project), TicketQueryEndpoints.Decoded(ticketId),
            MayReadTranscript(ctx), cancellationToken);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }

    internal static bool MayReadTranscript(HttpContext ctx)
    {
        if (!ctx.RequestServices.GetRequiredService<TokenAuthorityConfig>().Enforce) return true;
        return ctx.RequestServices.GetRequiredService<CallerIdentityResolver>()
            .Resolve(ctx.User).Permissions.Contains(Permissions.DialogWrite);
    }
}
