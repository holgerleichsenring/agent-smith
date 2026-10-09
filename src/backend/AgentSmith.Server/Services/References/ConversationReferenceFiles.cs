using System.Security.Claims;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-09-86e1: the files of a conversation's uploads as a caller on a dialog id may read them —
/// only the conversation open on that id, and only when the caller may reach it.
/// </summary>
public sealed class ConversationReferenceFiles(
    SpecDialogSessionRepository sessions, SpecDialogOwnership ownership, ReferenceSetRepository sets)
{
    /// <summary>The conversation open on <paramref name="dialogId"/>, or null when there is none the caller may reach.</summary>
    public async Task<string?> SessionOfAsync(string dialogId, ClaimsPrincipal user, CancellationToken ct)
    {
        var open = await sessions.GetOpenByThreadAsync(DispatcherDefaults.PlatformDashboard, dialogId, ct);
        return open is not null && SpecDialogOwnership.MayReach(open, ownership.OwnerOf(user)) ? open.SessionId : null;
    }

    /// <summary>One set's files with their sizes; empty for a set the conversation does not hold.</summary>
    public async Task<IReadOnlyList<ReferenceSetFileEntry>> EntriesAsync(
        string dialogId, string setId, ClaimsPrincipal user, CancellationToken ct) =>
        await SessionOfAsync(dialogId, user, ct) is { } session ? await sets.EntriesAsync(session, setId, ct) : [];

    /// <summary>One file's bytes, or null.</summary>
    public async Task<byte[]?> ContentAsync(
        string dialogId, string setId, string path, ClaimsPrincipal user, CancellationToken ct) =>
        await SessionOfAsync(dialogId, user, ct) is { } session ? await sets.FileAsync(session, setId, path, ct) : null;
}
