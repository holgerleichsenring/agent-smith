using System.Security.Claims;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-09-18-7a05: an operator deletes a design conversation they own. Addressed by its
/// SESSION id — a dialog id is only the tab it was last on — and carrying the surface's own
/// write permission, the shape every other destructive route but the run delete takes.
/// <para>
/// SUCCESS AND ALL THREE REFUSALS ANSWER ALIKE. Not yours, not there and already deleted are
/// one answer with a delete, so a second click is not an error and the route tells nobody
/// which session ids exist. The code is deliberately not 403: the authorization layer already
/// answers 403 for a missing permission, so a route whose ordinary answer were 403 would pass
/// its permission test with the permission requirement taken off it.
/// </para>
/// </summary>
internal static class SpecDialogDeletionEndpoints
{
    /// <summary>
    /// What a turn holding the gate is told. It borrows no sentence: the delete is refused
    /// because a turn is running, and that is what it says.
    /// </summary>
    private const string TurnIsRunning =
        "This conversation has a turn running. It can be deleted once the turn is over.";

    internal static WebApplication MapSpecDialogDeletionEndpoints(this WebApplication app)
    {
        app.MapDelete("/api/spec-dialog/conversations/{sessionId}", (Delegate)DeleteAsync)
           .Needs(Security.Permissions.DialogWrite);
        return app;
    }

    /// <summary>
    /// THE OWNER CHECK RUNS BEFORE THE HOLD IS TAKEN, and the ordering is load-bearing twice
    /// over. The held answer is this route's only asymmetry, so a hold taken first would tell
    /// any holder of the dialog permission whether a session id exists and is busy. Worse, the
    /// delete ACQUIRES the hold rather than reading it — a read is a check-then-act, and the
    /// router would enter a microsecond after the answer — so a non-owner reaching the acquire
    /// could take the gate on a session they do not own and bounce that owner's turns off it.
    /// <para>
    /// The acquire NARROWS the race rather than closing it: the router takes the hold only
    /// after the admission has read the session and appended a turn, so a message already
    /// inside the admission is invisible here. What is left degrades safely. The gate is in
    /// memory and per process, so a delete served by the replica that is not running the turn
    /// is not refused at all; closing that is named in the spec as a phase of its own.
    /// </para>
    /// </summary>
    internal static async Task<IResult> DeleteAsync(
        string sessionId,
        ClaimsPrincipal user,
        SpecDialogOwnership ownership,
        SpecDialogTurnGate turnGate,
        ISpecDialogConversationDeleter deleter,
        CancellationToken cancellationToken)
    {
        if (!await ownership.MayDeleteAsync(sessionId, ownership.OwnerOf(user), cancellationToken))
            return Results.NoContent();
        if (!turnGate.TryEnter(sessionId))
            return Results.Conflict(TurnIsRunning);
        try
        {
            await deleter.DeleteAsync(sessionId, cancellationToken);
        }
        finally
        {
            // Exiting an entry for a row that no longer exists is a harmless removal, and the
            // hold cannot outlive the process that took it.
            turnGate.Exit(sessionId);
        }
        return Results.NoContent();
    }
}
