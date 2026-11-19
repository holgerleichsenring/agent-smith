using System.Security.Claims;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Series;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-19-1b72a: the conversation head's Pause and Resume — the same <see cref="SeriesPause"/> the series master's
/// pause_series and resume_series take, so the button and the words are one act. A pause holds the series' starts, so
/// the caller holds runs.control as well as the dialog permission; the answer is the series as its pane now shows it.
/// </summary>
internal static class SpecDialogSeriesEndpoints
{
    internal static WebApplication MapSpecDialogSeriesEndpoints(this WebApplication app)
    {
        app.MapPost("/api/spec-dialog/{dialogId}/series/{act}", (Delegate)ActAsync)
           .Needs(Security.Permissions.DialogWrite, Security.Permissions.RunsControl).InProject(Security.ProjectFrom.Dialog);
        return app;
    }

    internal static async Task<IResult> ActAsync(
        string dialogId, string act, ClaimsPrincipal user, SpecDialogOwnership ownership, SpecDialogSessionRepository sessions,
        ISeriesRunStore store, SeriesPause pause, SeriesRunViewReader views, CancellationToken cancellationToken)
    {
        if (act is not ("pause" or "resume")) return Results.BadRequest($"'{act}' is not a series act; pause or resume.");
        var session = await sessions.GetOpenByThreadAsync(DispatcherDefaults.PlatformDashboard, dialogId, cancellationToken);
        if (session is null || !SpecDialogOwnership.MayReach(session, ownership.OwnerOf(user))) return Results.NotFound();
        if (await store.ActiveForConversationAsync(session.SessionId, cancellationToken) is not { } series)
            return Results.Conflict("This conversation has no running series.");
        _ = act == "pause"
            ? await pause.PauseAsync(series.Id, cancelRunning: false, cancellationToken)
            : await pause.ResumeAsync(series.Id, cancellationToken);
        return Results.Ok(await views.ReadAsync(dialogId, cancellationToken));
    }
}
