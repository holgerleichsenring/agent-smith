using AgentSmith.Domain.ValueObjects;

namespace AgentSmith.Domain.Entities;

/// <summary>
/// Represents a checked-out git repository.
/// </summary>
public sealed class Repository
{
    public string LocalPath { get; }
    public BranchName CurrentBranch { get; }
    public string RemoteUrl { get; }

    public Repository(string localPath, BranchName currentBranch, string remoteUrl)
    {
        LocalPath = localPath;
        CurrentBranch = currentBranch;
        RemoteUrl = remoteUrl;
    }
}
