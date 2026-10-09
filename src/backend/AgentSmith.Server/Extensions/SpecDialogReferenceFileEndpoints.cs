using System.Security.Claims;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.References;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-09-86e1: the files INSIDE a conversation's uploads — listed, previewed, served — and the
/// content hashes its sets hold, which the page compares a pick against before sending. Each read
/// is addressed by the dialog id and answers only for the conversation open on it that the caller
/// may reach (<see cref="ConversationReferenceFiles"/>); a set of another conversation, or a path
/// it does not hold, is not found.
/// <para>
/// THE BYTES ARE NEVER SERVED AS A PAGE. An uploaded .html or .svg opened from this origin would
/// run its script as the dashboard: the content route serves raster images as themselves and
/// everything else as an octet-stream attachment, never sniffed, under a sandboxing policy.
/// </para>
/// </summary>
internal static class SpecDialogReferenceFileEndpoints
{
    private const string Sandboxed = "sandbox";

    internal static WebApplication MapSpecDialogReferenceFileEndpoints(this WebApplication app)
    {
        const string files = "/api/spec-dialog/references/{setId}/files";
        app.MapGet("/api/spec-dialog/references/hashes", (Delegate)HashesAsync).Needs(Security.Permissions.DialogWrite);
        app.MapGet(files, (Delegate)FilesAsync).Needs(Security.Permissions.DialogWrite);
        app.MapGet($"{files}/preview", (Delegate)PreviewAsync).Needs(Security.Permissions.DialogWrite);
        app.MapGet($"{files}/content", (Delegate)ContentAsync).Needs(Security.Permissions.DialogWrite);
        return app;
    }

    internal static async Task<IResult> HashesAsync(string dialogId, ClaimsPrincipal user,
        ConversationReferenceFiles files, ConversationUploadAdmission admission, CancellationToken cancellationToken) =>
        await files.SessionOfAsync(dialogId, user, cancellationToken) is { } session
            ? Results.Ok(await admission.HeldAsync(session, cancellationToken))
            : Results.Ok(Array.Empty<HeldContentView>());

    internal static async Task<IResult> FilesAsync(string setId, string dialogId, ClaimsPrincipal user,
        ConversationReferenceFiles files, CancellationToken cancellationToken)
    {
        var entries = await files.EntriesAsync(dialogId, setId, user, cancellationToken);
        return entries.Count == 0
            ? Results.NotFound()
            : Results.Ok(entries.Select(f => new ReferenceFileView(f.Path, f.Bytes)).ToList());
    }

    internal static async Task<IResult> PreviewAsync(string setId, string dialogId, string path, ClaimsPrincipal user,
        ConversationReferenceFiles files, ReferenceFilePreview preview, CancellationToken cancellationToken) =>
        await files.ContentAsync(dialogId, setId, path, user, cancellationToken) is { } content
            ? Results.Ok(preview.For(path, content))
            : Results.NotFound();

    internal static async Task<IResult> ContentAsync(HttpContext http, string setId, string dialogId, string path,
        ConversationReferenceFiles files, ReferenceFilePreview preview, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        var content = await files.ContentAsync(dialogId, setId, path, http.User, cancellationToken);
        if (content is null) return Results.NotFound();
        http.Response.Headers.XContentTypeOptions = "nosniff";
        http.Response.Headers.ContentSecurityPolicy = Sandboxed;
        var type = preview.ServedAs(path);
        return type == ReferenceFileTypes.Untyped
            ? Results.File(content, type, path[(path.LastIndexOf('/') + 1)..])
            : Results.File(content, type);
    }
}
