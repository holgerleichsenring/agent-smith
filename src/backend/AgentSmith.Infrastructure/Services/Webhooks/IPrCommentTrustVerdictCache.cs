namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// Remembers a PR-comment author's trust verdict for a short while, so a burst of commands
/// on one pull request asks the code host once. A lookup that throws is not remembered.
/// </summary>
public interface IPrCommentTrustVerdictCache
{
    Task<bool> GetOrLookupAsync(string key, Func<Task<bool>> lookup);
}
