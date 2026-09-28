using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Opens a scoped spec-dialog session on a thread — the one act that creates a conversation.
/// <para>
/// 2026-09-22-2a86: the list, the resume and the fork left with the spellings that were their
/// only callers. What remains is reached by a typed "/spec" on chat and CONSTRUCTED by
/// <see cref="SpecDialogConversationResolver"/> for a page that already knows its project.
/// </para>
/// </summary>
public sealed class SpecDialogCommandHandler(
    SpecDialogSessionManager sessions,
    SpecDialogScopeResolver scopeResolver,
    SpecDialogReplyComposer composer,
    SpecDialogMessenger messenger)
{
    public Task HandleAsync(
        SpecCommand command, string userId, string channelId, string threadId,
        string platform, CancellationToken ct) => command switch
    {
        SpecOpenCommand open => HandleOpenAsync(
            open.Project, userId, channelId, threadId, platform, ct, open.Ticket),
        _ => throw new InvalidOperationException($"Unhandled spec command {command.GetType().Name}"),
    };

    private async Task HandleOpenAsync(
        string? project, string userId, string channelId, string threadId,
        string platform, CancellationToken ct, TicketBinding? ticket = null)
    {
        var existing = await sessions.GetOpenByThreadAsync(platform, threadId, ct);
        if (existing is not null)
        {
            await messenger.SendAsync(platform, channelId, threadId, composer.ComposeAlreadyOpen(existing), ct);
            return;
        }

        var resolution = scopeResolver.Resolve(project);
        // 2026-09-28-1da5b: opening is announced on CHAT only. There a command opens the dialog,
        // nothing renders a scope beside the thread, and the line is the sole acknowledgement that
        // anything happened. On the dashboard the first MESSAGE opens the conversation — so the
        // announcement would arrive after the operator's description, above the turn already
        // answering it, telling them to describe what they want to build. The SEND is skipped
        // rather than the text emptied: an empty push still resets the turn's working state on
        // arrival, and a composed reply cannot express "no message".
        if (resolution is ScopeResolved resolved)
        {
            var opened = await sessions.OpenAsync(
                platform, channelId, threadId, userId, resolved.Scope, ct, ticket);
            if (platform != DispatcherDefaults.PlatformDashboard)
                await messenger.SendAsync(
                    platform, channelId, threadId, composer.ComposeOpened(opened), ct);
            return;
        }

        var reply = resolution switch
        {
            ScopeChoiceRequired choice => composer.ComposeChoiceRequired(choice.Projects),
            ScopeUnknownProject unknown => composer.ComposeUnknownProject(unknown.Requested, unknown.Projects),
            var other => throw new InvalidOperationException($"Unhandled scope resolution {other.GetType().Name}"),
        };
        await messenger.SendAsync(platform, channelId, threadId, reply, ct);
    }
}
