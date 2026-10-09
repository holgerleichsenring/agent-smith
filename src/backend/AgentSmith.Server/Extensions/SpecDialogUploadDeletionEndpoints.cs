using System.Security.Claims;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-08-e8b9g: one upload taken back out of the conversation on a dialog id — a set, or an
/// image by the id the transcript addresses it by.
/// <para>
/// IT COPIES THE CONVERSATION DELETE'S ORDER. The reach check runs before the gate is taken (a
/// caller who may not reach the conversation learns nothing and holds nothing); the gate is
/// ACQUIRED, not read, and released in finally. A turn waiting for the person's approval still
/// holds it, which the view reports as idle — so the refusal names both.
/// </para>
/// <para>
/// A CITED UPLOAD IS REFUSED. The run of a filed ticket builds against what its approval cites,
/// and a run whose cited set is gone fails; the store re-checks after deleting (see
/// <see cref="ReferenceUploadDeletion"/>).
/// </para>
/// </summary>
internal static class SpecDialogUploadDeletionEndpoints
{
    internal const string TurnIsRunning =
        "A turn is running or waiting on your approval in this conversation. Remove the upload once it is over.";

    internal const string IsCited =
        "An approval of this conversation cites this upload — runs of the filed ticket build against it, "
        + "so it cannot be removed.";

    internal static WebApplication MapSpecDialogUploadDeletionEndpoints(this WebApplication app)
    {
        app.MapDelete("/api/spec-dialog/references/{setId}", (Delegate)DeleteSetAsync)
           .Needs(Security.Permissions.DialogWrite);
        app.MapDelete("/api/spec-dialog/images/{imageId:long}", (Delegate)DeleteImageAsync)
           .Needs(Security.Permissions.DialogWrite);
        return app;
    }

    internal static Task<IResult> DeleteSetAsync(
        string setId, string dialogId, ClaimsPrincipal user, SpecDialogOwnership ownership,
        SpecDialogSessionManager sessions, SpecDialogTurnGate turnGate, ReferenceUploadDeletion deletion,
        CancellationToken cancellationToken) =>
        RemoveAsync(dialogId, user, ownership, sessions, turnGate,
            session => deletion.DeleteSetAsync(session, setId, cancellationToken), cancellationToken);

    internal static Task<IResult> DeleteImageAsync(
        long imageId, string dialogId, ClaimsPrincipal user, SpecDialogOwnership ownership,
        SpecDialogSessionManager sessions, SpecDialogTurnGate turnGate, ReferenceUploadDeletion deletion,
        CancellationToken cancellationToken) =>
        RemoveAsync(dialogId, user, ownership, sessions, turnGate,
            session => deletion.DeleteImageAsync(session, imageId, cancellationToken), cancellationToken);

    private static async Task<IResult> RemoveAsync(
        string dialogId, ClaimsPrincipal user, SpecDialogOwnership ownership, SpecDialogSessionManager sessions,
        SpecDialogTurnGate turnGate, Func<string, Task<ReferenceUploadRemoval>> remove, CancellationToken ct)
    {
        var open = await sessions.GetOpenByThreadAsync(DispatcherDefaults.PlatformDashboard, dialogId, ct);
        if (open is null || !open.MayBeReachedBy(ownership.OwnerOf(user))) return Results.NotFound();
        if (!turnGate.TryEnter(open.JobId)) return Results.Conflict(TurnIsRunning);
        try
        {
            return await remove(open.JobId) switch
            {
                ReferenceUploadRemoval.Removed => Results.NoContent(),
                ReferenceUploadRemoval.Cited => Results.Conflict(IsCited),
                _ => Results.NotFound(),
            };
        }
        finally
        {
            turnGate.Exit(open.JobId);
        }
    }
}
