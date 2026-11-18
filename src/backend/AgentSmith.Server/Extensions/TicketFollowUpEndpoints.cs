using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets.FollowUps;

namespace AgentSmith.Server.Extensions;

/// <summary>2026-11-18-c8e2d: a person files a follow-up a spec's master proposed. runs.control, because it starts a
/// 'file' run that creates a tracker ticket. Every refusal is a plain sentence; only 200 is JSON.</summary>
internal static class TicketFollowUpEndpoints
{
    internal static WebApplication MapTicketFollowUpEndpoints(this WebApplication app)
    {
        app.MapPost("/api/tickets/{project}/{ticketId}/follow-ups", FileAsync)
            .Needs(Permissions.RunsControl).InProject(ProjectFrom.Route("project"));
        return app;
    }

    internal static async Task<IResult> FileAsync(
        string project, string ticketId, TicketFollowUpRequest request, HttpContext ctx, TicketFollowUpAct act,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var by = DisplayName.Of(ctx.User) ?? SpecDialogOwnership.PrincipalOf(ctx.User);
        var result = await act.FileAsync(
            TicketQueryEndpoints.Decoded(project), TicketQueryEndpoints.Decoded(ticketId), request, by, cancellationToken);
        return result.Refusal is { } sentence
            ? Results.Text(sentence, "text/plain", statusCode: result.Status)
            : Results.Ok(result.Body);
    }
}
