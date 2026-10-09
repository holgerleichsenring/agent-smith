namespace AgentSmith.Contracts.Sweep;

/// <summary>2026-10-08-10b0: a source provider that can list its repository's open pull requests.</summary>
public interface IOpenPullRequestSource
{
    IOpenPullRequestLister OpenPullRequests();
}
