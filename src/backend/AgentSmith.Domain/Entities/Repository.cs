using AgentSmith.Domain.Models;

namespace AgentSmith.Domain.Entities;

/// <summary>
/// A repository a run works on: the branch it is on and the remote it came from. Where it is
/// checked out is the sandbox's business, not the repository's (2026-11-25-9992c): a run's
/// repository lives at <see cref="WorkPathFor"/> inside the workspace mounted at
/// <see cref="WorkspaceRoot"/>, and a single-repo sandbox (a read-only source scope, a probe)
/// holds its tree at the root itself. A step asks its sandbox (ISandbox.WorkDirectory()).
/// </summary>
public sealed class Repository
{
    /// <summary>The workspace mount every sandbox container shares.</summary>
    public const string WorkspaceRoot = "/work";

    /// <summary>2026-11-25-9992c: THE one place a run repository's root is named.</summary>
    public static string WorkPathFor(string repoName) =>
        $"{WorkspaceRoot}/{repoName}";

    public BranchName CurrentBranch { get; }
    public string RemoteUrl { get; }

    public Repository(BranchName currentBranch, string remoteUrl)
    {
        CurrentBranch = currentBranch;
        RemoteUrl = remoteUrl;
    }
}
