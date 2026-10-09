using System.Security.Claims;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-08-e8b9g: one image into the conversation on a dialog id — its kind read from the
/// bytes, the conversation resolved or opened (an image pasted as the very first act is kept),
/// then held to the conversation's byte cap and refused when it is a copy of one it holds.
/// Extracted from the route, which keeps mapping and the bounded body read.
/// </summary>
public sealed class SpecDialogImageUpload(
    ImageKindFromBytes kinds,
    SpecDialogConversationResolver conversation,
    ReferenceFileRepository files,
    ConversationUploadAdmission admission)
{
    private const string NotYours =
        "This conversation belongs to someone else. Start one of your own to attach an image to it.";

    private const string NoConversation =
        "No conversation is open here and none could be started — name the project it is about.";

    private const string NotAnImage =
        "That file is not a PNG, JPEG, GIF or WebP image. The kind is read from the bytes, "
        + "not from what it is called.";

    public async Task<IResult> StoreAsync(
        string dialogId, string? project, ClaimsPrincipal user, byte[] bytes, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dialogId)) return Results.BadRequest("dialogId is required");
        if (kinds.Of(bytes) is not { } mediaType) return Results.BadRequest(NotAnImage);

        var target = await conversation.ResolveOrOpenAsync(dialogId, project, user, ct);
        if (target.BelongsToAnother) return Results.Conflict(NotYours);
        if (target.SessionId is not { } session) return Results.BadRequest(NoConversation);
        if (await admission.RefusalForImageAsync(session, bytes, ct) is { } refusal) return refusal.Answer();

        // 2026-10-01-283da: the bytes as bytes, an image a set of one.
        var stored = await files.AddAsync(
            new ReferenceFile
            {
                SessionId = session,
                SetId = Guid.NewGuid().ToString("N"),
                Kind = ReferenceFileKind.Image,
                MediaType = mediaType,
                Length = bytes.Length,
                Content = bytes,
                ContentSha256 = bytes.Sha256Hex(),
            },
            ct);
        return Results.Ok(new SpecDialogImageView(stored.Id, mediaType, stored.CreatedAt, stored.Length));
    }
}
