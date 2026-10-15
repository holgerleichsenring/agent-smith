using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Server.Services.Tickets.Text;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-14-dbcei: a person marks a ticket done by hand, with a note — runs.control, because the mark wakes and may
/// start every ticket that requires it. The tracker must know the ticket before the permanent write; after that the
/// write and its wake run to the end whatever the request does. Every refusal is a plain sentence; only 200 is JSON.
/// </summary>
internal static class TicketLandedByHandEndpoints
{
    internal const string NoteRequired = "A note is required: say why this ticket counts as landed.";

    internal static WebApplication MapTicketLandedByHandEndpoints(this WebApplication app)
    {
        app.MapPost("/api/tickets/{project}/{ticketId}/landed-by-hand", LandedByHandAsync).Needs(Permissions.RunsControl).InProject(ProjectFrom.Route("project"));
        return app.MapTicketCloseEndpoints();
    }

    // Internal so the endpoint tests drive the real recorder without a host.
    internal static async Task<IResult> LandedByHandAsync(
        string project, string ticketId, LandedByHandRequest request, HttpContext ctx, TicketProjects projects,
        ITicketProviderFactory trackers, ITicketLandingStore landings, TicketLandingRecorder recorder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(ctx);
        var (name, id) = (TicketQueryEndpoints.Decoded(project), TicketQueryEndpoints.Decoded(ticketId));
        if (projects.Of(name) is not { } resolved) return Say(StatusCodes.Status404NotFound, $"No project {name} is configured.");
        if (string.IsNullOrWhiteSpace(request.Note)) return Say(StatusCodes.Status400BadRequest, NoteRequired);
        var tracker = resolved.Tracker.Name;
        var key = TicketLandingRecorder.KeyOf(resolved, id);
        if ((await landings.LandedAsync(tracker, [key], cancellationToken)).FirstOrDefault() is { } standing)
            return Already(standing);
        try
        {
            await trackers.Create(resolved.Tracker).GetTicketAsync(new TicketId(id), cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            var reason = TicketReadFailureReason.Of(ex);
            return reason == TicketTextReasons.NotFound
                ? Say(StatusCodes.Status404NotFound, $"No ticket {id} on {tracker}.")
                : Say(StatusCodes.Status503ServiceUnavailable, $"The tracker {tracker} could not be read ({reason}); nothing was recorded.");
        }
        var by = DisplayName.Of(ctx.User) ?? SpecDialogOwnership.PrincipalOf(ctx.User);
        if (await recorder.RecordByHandAsync(resolved, id, by, request.Note, CancellationToken.None) is { } landing)
            return Results.Ok(landing);
        var winner = (await landings.LandedAsync(tracker, [key], CancellationToken.None)).FirstOrDefault();
        return winner is null ? Say(StatusCodes.Status409Conflict, $"{id} already counts as landed.") : Already(winner);
    }

    private static IResult Already(TicketLandingFact landing) => Say(StatusCodes.Status409Conflict,
        landing.Kind == TicketLandingKinds.ByHand
            ? $"{landing.TicketId} is already marked done by {landing.By} at {landing.LandedAt:u}: {landing.Note}."
            : $"{landing.TicketId} already counts as landed: merged on {landing.LandedAt:u}.");

    private static IResult Say(int status, string sentence) => Results.Text(sentence, "text/plain", statusCode: status);
}
