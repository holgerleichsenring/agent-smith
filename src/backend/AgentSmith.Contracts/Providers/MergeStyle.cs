namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-25-9992i: how a merge lands its commits. <see cref="Squash"/> is asked for every merge into a series branch, so
/// each ticket is one commit there — that branch carries no repository policy to override. Every other merge uses the
/// method the host's repository settings give it.
/// </summary>
public enum MergeStyle
{
    /// <summary>The repository's own method (GitHub: the first one it allows; GitLab and Azure DevOps: their defaults).</summary>
    HostDefault = 0,

    /// <summary>One commit for the whole pull request.</summary>
    Squash = 1,
}
