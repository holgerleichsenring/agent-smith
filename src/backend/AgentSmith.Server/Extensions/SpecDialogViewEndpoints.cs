using System.Security.Claims;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-09-15-cb3e: the dashboard dialog surface's read routes. The per-dialog read answers
/// for the dialog id the browser holds, which is why it carries the same permission as the
/// write route and then checks the OWNER: the id is minted in the browser and is no boundary
/// at all, so a caller who learned someone else's would otherwise read their transcript.
/// <para>
/// The conversation list takes no id: it answers for the signed-in principal alone. A past
/// conversation is opened by resuming it onto a fresh dialog id, where the owner check the
/// resume already makes applies, so no route reads a closed session's transcript.
/// </para>
/// </summary>
internal static class SpecDialogViewEndpoints
{
    internal static WebApplication MapSpecDialogViewEndpoints(this WebApplication app)
    {
        app.MapGet("/api/spec-dialog/conversations", (Delegate)ListAsync)
           .Needs(Security.Permissions.DialogWrite);
        app.MapGet("/api/spec-dialog/{dialogId}", (Delegate)ReadAsync)
           .Needs(Security.Permissions.DialogWrite);
        return app;
    }

    internal static async Task<IResult> ReadAsync(
        string dialogId,
        ClaimsPrincipal user,
        SpecDialogOwnership ownership,
        SpecDialogViewReader reader,
        CancellationToken cancellationToken)
    {
        var owner = ownership.OwnerOf(user);
        if (!await ownership.MayWatchAsync(dialogId, owner, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        return Results.Ok(await reader.ReadAsync(dialogId, cancellationToken));
    }

    internal static async Task<IResult> ListAsync(
        ClaimsPrincipal user,
        SpecDialogOwnership ownership,
        SpecDialogConversationList conversations,
        CancellationToken cancellationToken) =>
        Results.Ok(await conversations.ListAsync(ownership.OwnerOf(user), cancellationToken));
}
