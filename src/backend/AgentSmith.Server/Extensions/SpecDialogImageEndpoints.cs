using System.Security.Claims;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.References;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-09-20-3af8: an operator shows the design partner what they are looking at. One image
/// in, addressed by the dialog id the page holds; one image back out, addressed by its own id.
/// <para>
/// THE UPLOAD RESOLVES ITS SESSION AND OPENS ONE WHEN NONE IS OPEN. The message route
/// authorises through a check that passes when no open session is found — safe for a WATCH,
/// because nothing is delivered into an id nobody holds, and unsafe for a WRITE, which would
/// otherwise store rows keyed on no conversation, bounded by nothing and swept by no delete.
/// An absent session cannot simply refuse either: an image pasted as the very first act on a
/// fresh dialog id has no conversation yet, and a refusal would lose exactly the opening
/// screenshot. The upload therefore carries the project and resolves-or-opens through
/// <see cref="SpecDialogConversationResolver"/> — the same act the typed first message makes,
/// which is why neither has to be ordered against the other.
/// </para>
/// <para>
/// AND THE REFUSAL IS DISTINGUISHABLE, deliberately unlike the delete's three-way sameness. A
/// delete must not tell a stranger which conversations exist; an upload is to a conversation the
/// caller is in, and the page has to be able to tell "not yours" from "not allowed here at all"
/// to recover. It is not 403 either — the authorization layer already answers that for a missing
/// permission, and a route whose ordinary answer were 403 would pass its permission test with
/// the permission requirement taken off it.
/// </para>
/// </summary>
internal static class SpecDialogImageEndpoints
{
    internal static WebApplication MapSpecDialogImageEndpoints(this WebApplication app)
    {
        app.MapPost("/api/spec-dialog/images", (Delegate)UploadAsync)
           .Needs(Security.Permissions.DialogWrite);
        app.MapGet("/api/spec-dialog/images/{imageId:long}", (Delegate)ServeAsync)
           .Needs(Security.Permissions.DialogWrite);
        return app;
    }

    /// <summary>
    /// The body is the image itself and is never bound: it is bounded and read by
    /// <see cref="SpecDialogImageBody"/> before anything else touches it, so an over-size
    /// upload is refused without the bytes ever reaching this method.
    /// </summary>
    internal static async Task<IResult> UploadAsync(
        HttpContext http,
        string dialogId,
        string? project,
        SpecDialogImageBody body,
        SpecDialogImageUpload upload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        var bytes = await body.ReadAsync(http, cancellationToken);
        if (bytes is null) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        // 2026-10-08-e8b9g: the kind, the conversation, its cap and its copies are the upload's.
        return await upload.StoreAsync(dialogId, project, http.User, bytes, cancellationToken);
    }

    /// <summary>
    /// One stored image, for the transcript that shows it. An image nobody may see and an image
    /// that is not there answer alike: the id is a number a caller can guess.
    /// </summary>
    internal static async Task<IResult> ServeAsync(
        long imageId,
        ClaimsPrincipal user,
        SpecDialogOwnership ownership,
        ReferenceFileRepository files,
        CancellationToken cancellationToken)
    {
        // 2026-10-01-283da: a reference image, or the legacy row an id not copied yet names.
        var image = await files.GetAsync(imageId, cancellationToken);
        if (image is null) return Results.NotFound();
        if (!await ownership.OwnsAsync(image.SessionId, ownership.OwnerOf(user), cancellationToken))
            return Results.NotFound();
        return Results.File(image.Content, image.MediaType);
    }
}
