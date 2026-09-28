using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// Finds the configured repository a PR comment was made on, from the server's current
/// configuration. <c>null</c> when no project declares it.
/// </summary>
public interface IPrCommentRepoLookup
{
    ConfiguredRepo? Find(string repositoryUrl);
}
