using System.Security.Claims;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-20-3af8: which conversation a write on a dialog id is stored against — resolved, or
/// OPENED when none is open, and refused only when one is open that the caller does not own.
/// <para>
/// 2026-09-20-4b0aa: both writes a page makes on a fresh dialog id come through here. An image
/// upload and the operator's first typed message each carry the project the page holds, and each
/// needs the conversation to exist before it stores anything. Two posts cannot order themselves —
/// the message route answers before its turn has run — so the ordering lives here, inside the one
/// act that opens and then writes.
/// </para>
/// <para>
/// The opening goes through <see cref="SpecDialogCommandHandler"/>, whose own guard returns
/// WITHOUT opening when a session is already open on the thread, and which says for itself why it
/// could not open one — an unknown project, or several to choose between. The session manager's
/// open is never called here: opening over an existing session IS the fork, so a writer that lost
/// the race would close the conversation it was writing into.
/// </para>
/// </summary>
public sealed class SpecDialogConversationResolver(
    SpecDialogSessionManager sessions,
    SpecDialogOwnership ownership,
    SpecDialogCommandHandler commands)
{
    private const string Platform = DispatcherDefaults.PlatformDashboard;

    /// <summary>For a caller that arrives as a principal — an HTTP route with the request in hand.</summary>
    public Task<SpecDialogConversationTarget> ResolveOrOpenAsync(
        string dialogId, string? project, ClaimsPrincipal? caller, CancellationToken ct) =>
        ResolveOrOpenAsync(dialogId, project, ownership.OwnerOf(caller), ct);

    /// <summary>
    /// For a caller that already holds the owner — a dispatched task, which runs after the
    /// request that resolved the principal has ended.
    /// </summary>
    public async Task<SpecDialogConversationTarget> ResolveOrOpenAsync(
        string dialogId, string? project, string owner, CancellationToken ct)
    {
        var open = await sessions.GetOpenByThreadAsync(Platform, dialogId, ct);
        if (open is not null)
            return open.UserId == owner
                ? SpecDialogConversationTarget.On(open.JobId)
                : SpecDialogConversationTarget.Foreign;

        // The dialog id is both channel and thread here, as it is for every message the page
        // sends: a browser page holds one conversation and has no channel above it.
        await commands.HandleAsync(
            new SpecOpenCommand(project), owner, dialogId, dialogId, Platform, ct);
        var opened = await sessions.GetOpenByThreadAsync(Platform, dialogId, ct);
        return opened is null
            ? SpecDialogConversationTarget.Unopened
            : SpecDialogConversationTarget.On(opened.JobId);
    }
}
