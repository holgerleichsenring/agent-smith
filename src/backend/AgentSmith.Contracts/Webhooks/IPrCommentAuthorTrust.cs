namespace AgentSmith.Contracts.Webhooks;

/// <summary>
/// Answers whether the author of a pull-request / merge-request comment may write to the
/// repository the comment was made on. One implementation per code host, registered keyed
/// by platform name. A lookup that cannot answer answers <c>false</c>: a comment command
/// from someone the host cannot vouch for starts nothing and reaches no model.
/// </summary>
public interface IPrCommentAuthorTrust
{
    Task<bool> IsTrustedAsync(PrCommentAuthor author, CancellationToken cancellationToken);
}
