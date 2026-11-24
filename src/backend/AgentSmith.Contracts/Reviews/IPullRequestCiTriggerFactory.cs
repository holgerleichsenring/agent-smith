using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Reviews;

/// <summary>2026-11-24-f9c5d: the CI trigger of a repository's host, or null for a host with no pull requests (a local
/// working copy).</summary>
public interface IPullRequestCiTriggerFactory
{
    IPullRequestCiTrigger? Create(RepoConnection repo);
}
