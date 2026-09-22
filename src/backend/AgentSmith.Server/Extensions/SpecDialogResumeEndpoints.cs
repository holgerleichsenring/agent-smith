using System.Security.Claims;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-09-22-2a86: an operator continues a design conversation they own, in the tab they are
/// looking at. Addressed by the conversation's SESSION id and the dialog id it is to be moved
/// onto, carrying the surface's own write permission — the shape the deletion route already
/// takes. It replaces the "/spec resume &lt;id&gt;" the page used to post at its own server.
/// <para>
/// TWO OWNERS ARE CHECKED, NOT ONE. The resumer answers "not found" for a session that is not
/// the caller's, which is the source side. The TARGET dialog is the other side: the move
/// closes whatever is open on that thread before rebinding, so a route addressed by ids would
/// otherwise let a caller close another principal's live dialog with a conversation of their
/// own. That check used to be reached by parsing the posted spelling
/// (<see cref="SpecDialogOwnership.MayWatchAsync"/> on the dialog the text was posted into);
/// with the spelling gone the route makes it explicitly, BEFORE the resumer is called.
/// </para>
/// </summary>
internal static class SpecDialogResumeEndpoints
{
    /// <summary>What a conversation blocked on a question is answered with. It is where the
    /// question was asked that it has to be answered — moving the conversation would take the
    /// waiting gate away from the thread the answer is expected on.</summary>
    private const string QuestionIsWaiting =
        "This conversation is waiting for an answer where it is open. "
        + "Answer it there, then open the conversation here.";

    /// <summary>What a conversation mid-turn is answered with. The running turn holds the old
    /// thread: moved under it, its reply would find no open session and never be stored.</summary>
    private const string TurnIsRunning =
        "This conversation is in the middle of a turn. Open it here once that turn has replied.";

    internal static WebApplication MapSpecDialogResumeEndpoints(this WebApplication app)
    {
        app.MapPost("/api/spec-dialog/conversations/{sessionId}/resume", (Delegate)ResumeAsync)
           .Needs(Security.Permissions.DialogWrite);
        return app;
    }

    /// <summary>The dialog id the conversation is to be moved onto — the tab the page holds.
    /// It is both channel and thread here, as it is for every message the page sends: a browser
    /// page holds one conversation and has no channel above it.</summary>
    internal sealed record SpecDialogResumeRequest(string DialogId);

    internal static async Task<IResult> ResumeAsync(
        string sessionId,
        SpecDialogResumeRequest body,
        ClaimsPrincipal user,
        SpecDialogOwnership ownership,
        SpecDialogResumer resumer,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.DialogId))
            return Results.BadRequest("dialogId is required");

        var owner = ownership.OwnerOf(user);
        if (!await ownership.MayWatchAsync(body.DialogId, owner, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var resumed = await resumer.ResumeAsync(
            sessionId, owner, DispatcherDefaults.PlatformDashboard,
            body.DialogId, body.DialogId, cancellationToken);
        return resumed switch
        {
            SpecDialogResumed => Results.NoContent(),
            SpecDialogResumeRefused refused => Results.Conflict(
                refused.QuestionPending ? QuestionIsWaiting : TurnIsRunning),
            _ => Results.NotFound(),
        };
    }
}
