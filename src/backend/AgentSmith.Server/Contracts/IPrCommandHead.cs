using AgentSmith.Server.Services.Webhooks;

namespace AgentSmith.Server.Contracts;

/// <summary>
/// Completes a PR comment command whose delivery carried no head branch (GitHub's issue_comment) by
/// asking the configured repository's provider — only for a body that is a command, so an ordinary
/// comment costs no host call.
/// </summary>
public interface IPrCommandHead
{
    /// <summary>The command with its head, unchanged when it has one or is no command; null when the
    /// head could not be read — such a command starts nothing.</summary>
    Task<PrCommentCommand?> CompleteAsync(PrCommentCommand command, CancellationToken cancellationToken);
}
