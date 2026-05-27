using AgentSmith.Contracts.Webhooks;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Posts a reply to a PR/MR comment on the source platform.
/// </summary>
public interface IPrCommentReplyService
{
    Task ReplyAsync(CommentIntent originalComment, string text, CancellationToken cancellationToken);
}
