namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-17-023fa: the project's <c>merge:</c> block — whether a green code run merges its own pull
/// requests and whether it waits for the host's branch protection. Absent means manual, the behaviour
/// every project had before the block existed.
/// </summary>
public sealed class MergePolicyConfig
{
    public MergeWhen When { get; set; } = MergeWhen.Manual;

    /// <summary>
    /// False (the default): merge past branch policies, protection and approval rules, which are for people.
    /// True: arm the host's auto-complete or auto-merge and let its rules decide when.
    /// </summary>
    public bool RespectBranchProtection { get; set; }

    /// <summary>The protection mode a merge asks the provider for.</summary>
    public BranchProtection Protection => RespectBranchProtection ? BranchProtection.Respect : BranchProtection.Bypass;
}

/// <summary>2026-11-17-023fa: when a code run merges its pull requests. Values are stored as numbers.</summary>
public enum MergeWhen
{
    /// <summary>Pull requests stay open for a person.</summary>
    Manual = 0,

    /// <summary>A run whose every spec verified green merges its pull requests at its end.</summary>
    OnGateGreen = 1,
}

/// <summary>2026-11-17-023fa: whether a merge goes past the host's branch protection or waits for it.</summary>
public enum BranchProtection
{
    /// <summary>Merge now, past policies and approval rules, as the token's identity is allowed to.</summary>
    Bypass = 0,

    /// <summary>Arm auto-complete or auto-merge; the host merges when its rules pass.</summary>
    Respect = 1,
}
