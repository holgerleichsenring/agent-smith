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

    /// <summary>
    /// 2026-11-17-023fb: when a ticket of this project lands, merge its base into every other open agent-smith pull
    /// request on the same repository and base. Off by default: it pushes onto pull requests people may be reviewing.
    /// </summary>
    public bool RefreshOpenPullRequests { get; set; }

    /// <summary>
    /// 2026-11-17-3435a: how many characters of each failed check's evidence (the host's summary, then the end of its
    /// log) are read, stored and handed to a fix. 4000 holds a test failure's assertion and stack with room to spare
    /// and keeps a record row and a prompt section small.
    /// </summary>
    public int CiExcerptChars { get; set; } = 4000;

    /// <summary>
    /// 2026-11-17-3435a: how many failed checks of one red head have their logs read. Each costs a host call and a log
    /// download; five covers a matrix failing the same way, the rest are named with their links.
    /// </summary>
    public int CiFailedChecksRead { get; set; } = 5;

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
