namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0315b: a lazy, READ-ONLY sandbox over one repo of a spec-dialog scope.
/// Nothing is spawned until the first step arrives; the underlying container
/// is created on demand (generic git-bearing image, no toolchain build), the
/// repo is cloned once, and only content-read steps (ReadFile / ListFiles /
/// Grep / DirectoryTree) are served — Run and WriteFile come back as failed
/// step results. Disposal tears the materialised sandbox down; a sandbox
/// that never served a step disposes to nothing.
/// </summary>
public interface ISourceScopeSandbox : ISandbox
{
    /// <summary>The repo this sandbox grounds (the tool-host address name).</summary>
    string RepoName { get; }

    /// <summary>True once the underlying sandbox has been spawned + cloned.</summary>
    bool IsMaterialized { get; }

    /// <summary>
    /// 2026-09-13-9802: the commit this scope is actually on, once it has materialised —
    /// read from the clone, never echoed back from what was asked for. Null before the
    /// first read, and on a scope that asked for no revision and never looked.
    /// </summary>
    string? ResolvedSha { get; }
}
