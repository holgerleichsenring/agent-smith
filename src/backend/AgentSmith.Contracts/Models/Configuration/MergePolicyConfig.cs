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

    /// <summary>
    /// 2026-11-17-3435b: with on_ci_green, how often the open pull requests' CI is read. A minute keeps a green head
    /// merged within a minute and costs a few host calls per pull request per minute.
    /// </summary>
    public int CiPollSeconds { get; set; } = 60;

    /// <summary>
    /// 2026-11-17-3435b: how long a head with no CI reporting must stay so before it counts as having none — a host
    /// registers its checks seconds to minutes after a push, so 'nothing yet' is not 'nothing configured'.
    /// </summary>
    public int CiSettleSeconds { get; set; } = 180;

    /// <summary>
    /// 2026-11-17-3435b: after how long a head still pending is no longer read every ci_poll_seconds (a manual job or
    /// a stuck queue would poll forever); the five-minute re-read still sees it move. An hour outlasts a slow suite.
    /// </summary>
    public int CiPendingSeconds { get; set; } = 3600;

    /// <summary>
    /// 2026-11-17-3435b: the secret of the identity that approves a pull request the host requires a review for, with
    /// respect_branch_protection: true. A host refuses the author's own approval (always on GitHub, by setting on Azure
    /// DevOps and GitLab), and the author is the repo's own token. Null: approve as the repo's token where allowed.
    /// </summary>
    public string? ApprovalAuth { get; set; }

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

    /// <summary>2026-11-17-3435b: a gate-passed run's pull requests wait for the host's CI; green on every one merges them.</summary>
    OnCiGreen = 2,
}

/// <summary>2026-11-17-023fa: whether a merge goes past the host's branch protection or waits for it.</summary>
public enum BranchProtection
{
    /// <summary>Merge now, past policies and approval rules, as the token's identity is allowed to.</summary>
    Bypass = 0,

    /// <summary>Arm auto-complete or auto-merge; the host merges when its rules pass.</summary>
    Respect = 1,
}
