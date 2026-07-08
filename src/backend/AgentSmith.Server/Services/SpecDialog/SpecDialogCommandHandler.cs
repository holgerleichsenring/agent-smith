using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Executes the /spec commands: open a scoped session, list open sessions,
/// resume a session into the current thread, and fork ("/spec new").
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
        SpecListCommand => HandleListAsync(channelId, threadId, platform, ct),
        SpecResumeCommand resume => HandleResumeAsync(resume, channelId, threadId, platform, ct),
        SpecOpenCommand open => HandleOpenAsync(open.Project, forceNew: false, userId, channelId, threadId, platform, ct),
        SpecNewCommand fork => HandleOpenAsync(fork.Project, forceNew: true, userId, channelId, threadId, platform, ct),
        _ => throw new InvalidOperationException($"Unhandled spec command {command.GetType().Name}"),
    };

    private async Task HandleListAsync(
        string channelId, string threadId, string platform, CancellationToken ct)
    {
        var open = await sessions.ListOpenAsync(platform, ct);
        await messenger.SendAsync(platform, channelId, threadId, composer.ComposeList(open), ct);
    }

    private async Task HandleResumeAsync(
        SpecResumeCommand resume, string channelId, string threadId,
        string platform, CancellationToken ct)
    {
        if (resume.SessionId.Length == 0)
        {
            await messenger.SendAsync(platform, channelId, threadId, composer.ComposeResumeUsage(), ct);
            return;
        }

        var state = await sessions.ResumeAsync(resume.SessionId, platform, channelId, threadId, ct);
        var reply = state is null
            ? composer.ComposeSessionNotFound(resume.SessionId)
            : composer.ComposeResumed(state);
        await messenger.SendAsync(platform, channelId, threadId, reply, ct);
    }

    private async Task HandleOpenAsync(
        string? project, bool forceNew, string userId, string channelId,
        string threadId, string platform, CancellationToken ct)
    {
        var existing = await sessions.GetOpenByThreadAsync(platform, threadId, ct);
        if (existing is not null && !forceNew)
        {
            await messenger.SendAsync(platform, channelId, threadId, composer.ComposeAlreadyOpen(existing), ct);
            return;
        }

        var reply = ResolveScope(project, existing) switch
        {
            ScopeResolved resolved => composer.ComposeOpened(
                await sessions.OpenAsync(platform, channelId, threadId, userId, resolved.Scope, ct)),
            ScopeChoiceRequired choice => composer.ComposeChoiceRequired(choice.Projects),
            ScopeUnknownProject unknown => composer.ComposeUnknownProject(unknown.Requested, unknown.Projects),
            var other => throw new InvalidOperationException($"Unhandled scope resolution {other.GetType().Name}"),
        };
        await messenger.SendAsync(platform, channelId, threadId, reply, ct);
    }

    // A fork without an explicit pick keeps the current session's scope.
    private ScopeResolution ResolveScope(string? project, ConversationState? existing) =>
        project is null && existing?.Scope is { } scope
            ? new ScopeResolved(scope)
            : scopeResolver.Resolve(project);
}
