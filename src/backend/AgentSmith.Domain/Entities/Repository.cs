using AgentSmith.Domain.Models;

namespace AgentSmith.Domain.Entities;

/// <summary>
/// Represents a repository checked out into the pipeline sandbox. The sandbox
/// root is fixed at '/work' (SandboxWorkPath). Under p0158e (multi-sandbox-
/// per-repo), each repo has its OWN sandbox where /work is that repo's root —
/// so LocalPath is `/work` regardless of repo name; repo-prefixed addressing
/// (`server/src/...`) happens at the tool-host layer, not inside the sandbox.
/// The static WorkPathFor helper remains for tool-host external addressing
/// (composing the operator-facing path from the run's perspective).
/// </summary>
public sealed class Repository
{
    public const string SandboxWorkPath = "/work";

    /// <summary>
    /// External (orchestrator-facing) path for a named repo. Used by tool
    /// hosts when rendering repo-prefixed output (e.g. grep results across
    /// all repos). NOT used as a sandbox-internal working directory.
    /// </summary>
    public static string WorkPathFor(string repoName) =>
        $"{SandboxWorkPath}/{repoName}";

    public string LocalPath => SandboxWorkPath;
    public BranchName CurrentBranch { get; }
    public string RemoteUrl { get; }

    public Repository(BranchName currentBranch, string remoteUrl)
    {
        CurrentBranch = currentBranch;
        RemoteUrl = remoteUrl;
    }
}
