namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>2026-11-17-023fa: whether a merge goes past the host's branch protection or waits for it.</summary>
public enum BranchProtection
{
    /// <summary>Merge now, past policies and approval rules, as the token's identity is allowed to.</summary>
    Bypass = 0,

    /// <summary>Arm auto-complete or auto-merge; the host merges when its rules pass.</summary>
    Respect = 1,
}
