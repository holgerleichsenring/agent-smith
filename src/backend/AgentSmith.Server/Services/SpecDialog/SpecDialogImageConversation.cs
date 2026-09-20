using System.Security.Claims;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-20-3af8: which conversation an uploaded image is stored against — resolved, or
/// OPENED when none is open, and refused only when one is open that the caller does not own.
/// <para>
/// An absent session cannot refuse, because of a race the page walks into on its very first
/// message: it opens a conversation by posting the open command to the message route, which
/// answers before the session exists, and the upload is a second, unordered background post. A
/// refusal would lose exactly the opening screenshot.
/// </para>
/// <para>
/// The opening goes through <see cref="SpecDialogCommandHandler"/>, whose own guard returns
/// WITHOUT opening when a session is already open on the thread. The session manager's open is
/// never called here: opening over an existing session IS the fork, so an upload that lost the
/// race would close the conversation it was uploading into.
/// </para>
/// </summary>
public sealed class SpecDialogImageConversation(
    SpecDialogSessionManager sessions,
    SpecDialogOwnership ownership,
    SpecDialogCommandHandler commands)
{
    private const string Platform = DispatcherDefaults.PlatformDashboard;

    public async Task<SpecDialogUploadTarget> ResolveOrOpenAsync(
        string dialogId, string? project, ClaimsPrincipal? caller, CancellationToken ct)
    {
        var owner = ownership.OwnerOf(caller);
        var open = await sessions.GetOpenByThreadAsync(Platform, dialogId, ct);
        if (open is not null)
            return open.UserId == owner
                ? SpecDialogUploadTarget.On(open.JobId)
                : SpecDialogUploadTarget.Foreign;

        // The dialog id is both channel and thread here, as it is for every message the page
        // sends: a browser page holds one conversation and has no channel above it.
        await commands.HandleAsync(
            new SpecOpenCommand(project), owner, dialogId, dialogId, Platform, ct);
        var opened = await sessions.GetOpenByThreadAsync(Platform, dialogId, ct);
        return opened is null ? SpecDialogUploadTarget.Unopened : SpecDialogUploadTarget.On(opened.JobId);
    }
}
