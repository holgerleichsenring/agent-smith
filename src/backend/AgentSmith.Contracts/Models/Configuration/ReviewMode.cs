namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-25-9992i: when a person looks at a project's work — the one merge-decision setting (<c>review:</c>), replacing
/// four merge keys that each decided a piece of it.
/// </summary>
public enum ReviewMode
{
    /// <summary>A series integrates on its series branch and a person reviews its final pull request per repository; a
    /// standalone ticket's pull request is left for a person.</summary>
    End = 0,

    /// <summary>No series branch: a person approves each ticket's pull request into the target; a standalone ticket's pull
    /// request is left for a person.</summary>
    Each = 1,

    /// <summary>No series branch: the series master merges each ticket on green; a standalone ticket merges on CI green under
    /// the host's branch protection (auto-merge).</summary>
    None = 2,
}
