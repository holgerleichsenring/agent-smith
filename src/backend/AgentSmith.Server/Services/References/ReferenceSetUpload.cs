using System.Security.Claims;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-01-283db: one upload into the conversation on a dialog id — unpacked when it came as
/// one archive, checked whole, then stored as ONE set in one save. The conversation is resolved
/// or opened the way an image upload does it, so a site dropped as the very first act is kept.
/// 2026-10-02-0d72: every refusal is logged once at Warning with its reason — it is the answer an
/// operator looks for when the page could not show it.
/// </summary>
public sealed class ReferenceSetUpload(
    ReferenceZipReader archives,
    ReferenceSetValidator validator,
    ReferenceFileTypes types,
    SpecDialogConversationResolver conversation,
    ReferenceSetRepository sets,
    ILogger<ReferenceSetUpload> logger)
{
    private const string NotYours =
        "This conversation belongs to someone else. Start one of your own to attach files to it.";

    private const string NoConversation =
        "No conversation is open here and none could be started — name the project it is about.";

    public async Task<IResult> StoreAsync(
        string dialogId, string? project, ClaimsPrincipal user,
        IReadOnlyList<ReferenceUploadPart> parts, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (string.IsNullOrWhiteSpace(dialogId)) return Refused("(none)", "dialogId is required");
        if (parts.Count == 0) return Refused(dialogId, "The upload carries no file.");
        var unpacked = Unpacked(parts);
        var check = unpacked.IsRefused ? unpacked : validator.Check(unpacked.Files);
        if (check.Refusal is { } refused) return Refused(dialogId, refused);

        var target = await conversation.ResolveOrOpenAsync(dialogId, project, user, ct);
        if (target.BelongsToAnother) return Refused(dialogId, NotYours, Results.Conflict(NotYours));
        if (target.SessionId is not { } session) return Refused(dialogId, NoConversation);
        if ((await sets.ListAsync(session, ct)).Count >= ReferenceUploadLimits.MaxSetsPerConversation)
            return Refused(dialogId, $"This conversation already holds "
                + $"{ReferenceUploadLimits.MaxSetsPerConversation} uploads, the per-conversation limit.");

        var stored = await sets.AddAsync(session, [.. check.Files.Select(Row)], ct);
        return Stored(dialogId, stored, ReferenceKeepRule.Collapsed([.. unpacked.LeftOut, .. check.LeftOut]),
            [.. check.Files.Select(f => f.Path).Where(ReferenceCredentialFiles.Holds)]);
    }

    private IResult Stored(
        string dialogId, ReferenceSetSummary stored, IReadOnlyList<ReferenceLeftOut> leftOut, IReadOnlyList<string> credentials)
    {
        if (leftOut.Count > 0)
            logger.LogInformation("An upload on dialog {DialogId} stored {Files} file(s) and left out {LeftOut} "
                + "entr(ies).", dialogId, stored.Files, leftOut.Count);
        return Results.Ok(new ReferenceUploadView(stored.SetId, stored.Name, stored.Files, stored.Bytes, stored.At,
            [.. leftOut.Take(ReferenceUploadLimits.MaxLeftOutListed)], leftOut.Count, credentials));
    }

    private IResult Refused(string dialogId, string reason, IResult? answer = null)
    {
        logger.LogWarning("An upload on dialog {DialogId} was refused: {Reason}", dialogId, reason);
        return answer ?? Results.BadRequest(reason);
    }

    // Exactly one archive is a delivery form and is unpacked; an archive among other files is
    // just a file, and is kept as one.
    private ReferenceSetCheck Unpacked(IReadOnlyList<ReferenceUploadPart> parts) =>
        parts is [var only] && ReferenceZipReader.IsArchive(only.Path)
            ? archives.Read(only.Path, only.Content)
            : new ReferenceSetCheck(parts, null);

    private ReferenceFile Row(ReferenceUploadPart part) => new()
    {
        RelativePath = part.Path,
        MediaType = types.MediaTypeOf(part.Path),
        Content = part.Content,
    };
}
