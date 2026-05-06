using AgentSmith.Domain.Models;

namespace AgentSmith.Domain.Entities;

/// <summary>
/// Represents a repository checked out into the pipeline sandbox. LocalPath is
/// fixed to the sandbox working directory ('/work'); all server-side filesystem
/// concerns route through ISandbox / SandboxFileReader.
/// </summary>
public sealed class Repository
{
    public const string SandboxWorkPath = "/work";

    public string LocalPath => SandboxWorkPath;
    public BranchName CurrentBranch { get; }
    public string RemoteUrl { get; }

    public Repository(BranchName currentBranch, string remoteUrl)
    {
        CurrentBranch = currentBranch;
        RemoteUrl = remoteUrl;
    }
}
