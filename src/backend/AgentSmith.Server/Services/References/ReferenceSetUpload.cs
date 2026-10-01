using System.Security.Claims;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-01-283db: one website into the conversation on a dialog id — unpacked when it came as
/// one archive, checked whole, then stored as ONE set in one save. The conversation is resolved
/// or opened the way an image upload does it, so a site dropped as the very first act is kept.
/// </summary>
public sealed class ReferenceSetUpload(
    ReferenceZipReader archives,
    ReferenceSetValidator validator,
    ReferenceFileTypes types,
    SpecDialogConversationResolver conversation,
    ReferenceSetRepository sets)
{
    private const string NotYours =
        "This conversation belongs to someone else. Start one of your own to attach a website to it.";

    private const string NoConversation =
        "No conversation is open here and none could be started — name the project it is about.";

    public async Task<IResult> StoreAsync(
        string dialogId, string? project, ClaimsPrincipal user,
        IReadOnlyList<ReferenceUploadPart> parts, CancellationToken ct)
    {
        var unpacked = Unpacked(parts);
        var check = unpacked.IsRefused ? unpacked : validator.Check(unpacked.Files);
        if (check.Refusal is { } refused) return Results.BadRequest(refused);

        var target = await conversation.ResolveOrOpenAsync(dialogId, project, user, ct);
        if (target.BelongsToAnother) return Results.Conflict(NotYours);
        if (target.SessionId is not { } session) return Results.BadRequest(NoConversation);
        if ((await sets.ListAsync(session, ct)).Count >= ReferenceUploadLimits.MaxSetsPerConversation)
            return Results.BadRequest($"This conversation already holds "
                + $"{ReferenceUploadLimits.MaxSetsPerConversation} websites, the per-conversation limit.");

        var stored = await sets.AddAsync(session, [.. check.Files.Select(Row)], ct);
        return Results.Ok(new ReferenceSetView(stored.SetId, stored.Name, stored.Files, stored.Bytes, stored.At));
    }

    // Exactly one archive is a delivery form and is unpacked; an archive among other files is
    // just a file, and the extension rule refuses it.
    private ReferenceSetCheck Unpacked(IReadOnlyList<ReferenceUploadPart> parts) =>
        parts is [var only] && ReferenceZipReader.IsArchive(only.Path)
            ? archives.Read(only.Path, only.Content)
            : new ReferenceSetCheck(parts, null);

    private ReferenceFile Row(ReferenceUploadPart part) => new()
    {
        RelativePath = part.Path,
        MediaType = types.MediaTypeOf(part.Path)!,
        Content = part.Content,
    };
}
