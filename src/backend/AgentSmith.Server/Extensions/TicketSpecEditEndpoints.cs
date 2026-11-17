using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets.Editing;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-17-dd05c: a person edits one spec of a filed ticket from its page — runs.control, because the edit is an
/// approval of the series as it now reads. Every answer is a plain sentence: what was recorded, or why nothing was.
/// </summary>
internal static class TicketSpecEditEndpoints
{
    internal static WebApplication MapTicketSpecEditEndpoints(this WebApplication app)
    {
        app.MapPut("/api/tickets/{project}/{ticketId}/specs/{specId}", EditAsync)
            .Needs(Permissions.RunsControl).InProject(ProjectFrom.Route("project"));
        return app;
    }

    // Internal so the endpoint tests drive the real edit without a host.
    internal static async Task<IResult> EditAsync(
        string project, string ticketId, string specId, TicketSpecEditRequest request, HttpContext ctx, TicketSpecEdit edit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (string.IsNullOrWhiteSpace(request?.Yaml))
            return Results.Text("The edit carries no spec yaml. Nothing was recorded.", "text/plain", statusCode: StatusCodes.Status400BadRequest);
        var by = DisplayName.Of(ctx.User) ?? SpecDialogOwnership.PrincipalOf(ctx.User);
        var answer = await edit.EditAsync(TicketQueryEndpoints.Decoded(project), TicketQueryEndpoints.Decoded(ticketId),
            TicketQueryEndpoints.Decoded(specId), request.Yaml, by, cancellationToken);
        return Results.Text(answer.Message, "text/plain", statusCode: answer.Status);
    }
}
