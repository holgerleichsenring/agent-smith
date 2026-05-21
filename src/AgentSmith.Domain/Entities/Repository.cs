using AgentSmith.Domain.Models;

namespace AgentSmith.Domain.Entities;

/// <summary>
/// Represents a repository checked out into the pipeline sandbox. The sandbox
/// root is fixed at '/work' (SandboxWorkPath); under the p0158 unified-run
/// model each configured repo lives at WorkPathFor(name) = '/work/{name}'.
/// LocalPath carries the per-instance path so handlers iterating multiple
/// repos can address each one explicitly.
/// </summary>
public sealed class Repository
{
    public const string SandboxWorkPath = "/work";

    /// <summary>
    /// In-sandbox path for a named repo. Single source of truth for the
    /// per-repo subdirectory under SandboxWorkPath.
    /// </summary>
    public static string WorkPathFor(string repoName) =>
        $"{SandboxWorkPath}/{repoName}";

    public string LocalPath { get; }
    public BranchName CurrentBranch { get; }
    public string RemoteUrl { get; }

    public Repository(BranchName currentBranch, string remoteUrl, string? localPath = null)
    {
        CurrentBranch = currentBranch;
        RemoteUrl = remoteUrl;
        LocalPath = localPath ?? SandboxWorkPath;
    }
}
