using System.Security.Claims;
using AgentSmith.Server.Security;
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
           .Needs(Security.Permissions.DialogWrite).InProject(Security.ProjectFrom.AcrossProjects);
        app.MapGet("/api/spec-dialog/{dialogId}", (Delegate)ReadAsync)
           .Needs(Security.Permissions.DialogWrite).InProject(Security.ProjectFrom.Dialog);
        // 2026-09-17-042ej: the rows are RUN STATE, so the caller holds runs.read as well as the
        // dialog permission, and the owner check below still decides WHICH conversation.
        app.MapGet("/api/spec-dialog/{dialogId}/filed-work", (Delegate)ReadFiledWorkAsync)
           .Needs(Security.Permissions.DialogWrite, Security.Permissions.RunsRead).InProject(Security.ProjectFrom.Dialog);
        return app;
    }

    internal static async Task<IResult> ReadAsync(
        string dialogId,
        ClaimsPrincipal user,
        SpecDialogOwnership ownership,
        SpecDialogViewReader reader,
        CallerProjectScope scopes,
        CancellationToken cancellationToken)
    {
        var owner = ownership.OwnerOf(user);
        if (!await ownership.MayWatchAsync(dialogId, owner, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var view = await reader.ReadAsync(dialogId, cancellationToken);
        // 2026-10-15-4a4dd: a new conversation is offered only the projects where the caller holds dialog.write.
        var scope = scopes.For(user, Security.Permissions.DialogWrite);
        return Results.Ok(scope.Only is null ? view : view with { Projects = [.. view.Projects.Where(p => scope.Admits(p.Name))] });
    }

    /// <summary>
    /// 2026-09-17-042ej: the work the conversation filed, as its runs now stand. A dialog id
    /// with no open session reads empty — the same rule that lets the watch through.
    /// </summary>
    internal static async Task<IResult> ReadFiledWorkAsync(
        string dialogId,
        ClaimsPrincipal user,
        SpecDialogOwnership ownership,
        FiledWorkReader filedWork,
        CallerProjectScope scopes,
        CancellationToken cancellationToken)
    {
        var owner = ownership.OwnerOf(user);
        if (!await ownership.MayWatchAsync(dialogId, owner, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        return Results.Ok(await filedWork.ReadAsync(dialogId, scopes.For(user, Security.Permissions.RunsRead), cancellationToken));
    }

    /// <summary>
    /// 2026-09-21-f237b: the limit is the caller's, clamped between one and the list's ceiling —
    /// the panel reads its default, the conversations page asks for the ceiling. Clamped rather
    /// than refused, which is what the other limit-taking routes here do; a limit reaches no
    /// further than how MANY rows are served, never which, because the owner is taken from the
    /// signed-in principal below and from nowhere else.
    /// </summary>
    internal static async Task<IResult> ListAsync(
        int? limit,
        ClaimsPrincipal user,
        SpecDialogOwnership ownership,
        SpecDialogConversationList conversations,
        CallerProjectScope scopes,
        CancellationToken cancellationToken) =>
        Results.Ok(await conversations.ListAsync(
            ownership.OwnerOf(user), scopes.For(user, Security.Permissions.DialogWrite), limit, cancellationToken));
}
