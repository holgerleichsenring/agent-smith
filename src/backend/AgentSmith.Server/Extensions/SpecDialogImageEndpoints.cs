using System.Security.Claims;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
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
    private const string NotYours =
        "This conversation belongs to someone else. Start one of your own to attach an image to it.";

    private const string NoConversation =
        "No conversation is open here and none could be started — name the project it is about.";

    private const string NotAnImage =
        "That file is not a PNG, JPEG, GIF or WebP image. The kind is read from the bytes, "
        + "not from what it is called.";

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
        ImageKindFromBytes kinds,
        SpecDialogConversationResolver conversation,
        SpecDialogAttachmentRepository attachments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        var bytes = await body.ReadAsync(http, cancellationToken);
        if (bytes is null) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        if (string.IsNullOrWhiteSpace(dialogId)) return Results.BadRequest("dialogId is required");
        if (kinds.Of(bytes) is not { } mediaType) return Results.BadRequest(NotAnImage);

        var target = await conversation.ResolveOrOpenAsync(
            dialogId, project, http.User, cancellationToken);
        if (target.BelongsToAnother) return Results.Conflict(NotYours);
        if (target.SessionId is not { } session) return Results.BadRequest(NoConversation);

        var stored = await attachments.AddAsync(
            new SpecDialogAttachment
            {
                SessionId = session,
                MediaType = mediaType,
                ContentBase64 = Convert.ToBase64String(bytes),
            },
            cancellationToken);
        return Results.Ok(new SpecDialogImageView(stored.Id, mediaType, stored.CreatedAt));
    }

    /// <summary>
    /// One stored image, for the transcript that shows it. An image nobody may see and an image
    /// that is not there answer alike: the id is a number a caller can guess.
    /// </summary>
    internal static async Task<IResult> ServeAsync(
        long imageId,
        ClaimsPrincipal user,
        SpecDialogOwnership ownership,
        SpecDialogAttachmentRepository attachments,
        CancellationToken cancellationToken)
    {
        var image = await attachments.GetAsync(imageId, cancellationToken);
        if (image is null) return Results.NotFound();
        if (!await ownership.OwnsAsync(image.SessionId, ownership.OwnerOf(user), cancellationToken))
            return Results.NotFound();
        return Results.File(Convert.FromBase64String(image.ContentBase64), image.MediaType);
    }
}
