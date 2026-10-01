using System.Security.Claims;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.References;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-01-283db: an operator drops a website into a design conversation — several files, a
/// folder, or one ZIP — and it is stored as ONE set of files with their paths, inside limits
/// stated once in <see cref="ReferenceUploadLimits"/>. The upload resolves or opens the
/// conversation as the image upload does, and answers alike: 409 for someone else's, 400 for none.
/// </summary>
internal static class SpecDialogReferenceEndpoints
{
    private const string TooLarge =
        "The upload is over the 26 MB ceiling for one website, or holds too many parts.";

    internal static WebApplication MapSpecDialogReferenceEndpoints(this WebApplication app)
    {
        app.MapPost("/api/spec-dialog/references", (Delegate)UploadAsync)
           .Needs(Security.Permissions.DialogWrite);
        app.MapGet("/api/spec-dialog/references", (Delegate)ListAsync)
           .Needs(Security.Permissions.DialogWrite);
        return app;
    }

    /// <summary>The body is never bound: <see cref="ReferenceUploadBody"/> bounds it before it is read.</summary>
    internal static async Task<IResult> UploadAsync(
        HttpContext http, string dialogId, string? project,
        ReferenceUploadBody body, ReferenceSetUpload upload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        var parts = await body.ReadAsync(http, cancellationToken);
        if (parts is null) return Results.Json(TooLarge, statusCode: StatusCodes.Status413PayloadTooLarge);
        if (string.IsNullOrWhiteSpace(dialogId)) return Results.BadRequest("dialogId is required");
        if (parts.Count == 0) return Results.BadRequest("The upload carries no file.");
        return await upload.StoreAsync(dialogId, project, http.User, parts, cancellationToken);
    }

    /// <summary>The conversation's websites on this dialog id; a dialog id with none open reads empty.</summary>
    internal static async Task<IResult> ListAsync(
        string dialogId, ClaimsPrincipal user, SpecDialogOwnership ownership,
        SpecDialogSessionRepository sessions, ReferenceSetRepository sets, CancellationToken cancellationToken)
    {
        if (!await ownership.MayWatchAsync(dialogId, ownership.OwnerOf(user), cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var open = await sessions.GetOpenByThreadAsync(DispatcherDefaults.PlatformDashboard, dialogId, cancellationToken);
        if (open is null) return Results.Ok(Array.Empty<ReferenceSetView>());
        return Results.Ok((await sets.ListAsync(open.SessionId, cancellationToken))
            .Select(s => new ReferenceSetView(s.SetId, s.Name, s.Files, s.Bytes, s.At)).ToList());
    }
}
