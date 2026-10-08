using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Webhooks;

namespace AgentSmith.Server.Contracts;

/// <summary>
/// 2026-10-08-e8b9e: starts what an admitted PR comment command asked for — a ticket's run through the
/// claim, a ticketless run through the chat launcher — and answers on the pull request.
/// </summary>
public interface IPrCommandLaunch
{
    /// <param name="allowedProjects">2026-10-08-10b0: when given, the owner is chosen among these only
    /// (the PR sweep's polling entry); a project named outside them starts nothing.</param>
    Task<WebhookResult> LaunchAsync(
        PrCommentCommand command, PipelineRequest request, CancellationToken cancellationToken,
        IReadOnlySet<string>? allowedProjects = null);
}
