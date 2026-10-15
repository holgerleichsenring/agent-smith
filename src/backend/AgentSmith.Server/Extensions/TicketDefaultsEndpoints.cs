using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets.Defaults;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-13-8762p: a ticket's defaults — read with runs.read like the rest of the ticket surface (a design
/// conversation's text only with dialog.write), decided in batch with runs.control, because an overrule starts
/// work like a Retry. 404 an unknown ticket, 400 a refused batch as plain text, 200 what was applied.
/// </summary>
internal static class TicketDefaultsEndpoints
{
    internal static WebApplication MapTicketDefaultsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tickets/{project}/{ticketId}/defaults", DefaultsAsync).Needs(Permissions.RunsRead).InProject(ProjectFrom.Route("project"));
        app.MapPost("/api/tickets/{project}/{ticketId}/defaults/verdicts", VerdictsAsync).Needs(Permissions.RunsControl).InProject(ProjectFrom.Route("project"));
        return app.MapTicketLandedByHandEndpoints();
    }

    // Internal so the endpoint tests drive the real reader and writer without a host.
    internal static async Task<IResult> DefaultsAsync(
        string project, string ticketId, HttpContext ctx, TicketDefaultsReader reader, CancellationToken cancellationToken)
    {
        var view = await reader.ReadAsync(TicketQueryEndpoints.Decoded(project), TicketQueryEndpoints.Decoded(ticketId),
            TicketConversationReadEndpoints.MayReadTranscript(ctx), cancellationToken);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }

    internal static async Task<IResult> VerdictsAsync(
        string project, string ticketId, List<DefaultVerdictRequest>? items, HttpContext ctx, DefaultVerdictWriter writer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var outcome = await writer.WriteAsync(TicketQueryEndpoints.Decoded(project), TicketQueryEndpoints.Decoded(ticketId),
            items, SpecDialogOwnership.PrincipalOf(ctx.User), cancellationToken);
        if (outcome.Unknown) return Results.NotFound();
        return outcome.Refusal is { } refusal ? Results.BadRequest(refusal) : Results.Ok(outcome.Result);
    }
}
