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
    /// 2026-11-24-f9c5a: when a ticket pull request of a series merges into its series branch; <see cref="When"/> keeps
    /// governing every pull request into the default branch, the series' final one included. On green CI by default
    /// (operator: a series runs through); manual lets a team review every ticket.
    /// </summary>
    public MergeWhen SeriesWhen { get; set; } = MergeWhen.OnCiGreen;

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
    /// a stuck queue would poll forever); the periodic PR re-read still sees it move (2026-11-22-6d85a: in polling mode too,
    /// every tickets.pr_reread_stale_seconds). An hour outlasts a slow suite.
    /// </summary>
    public int CiPendingSeconds { get; set; } = 3600;

    /// <summary>
    /// 2026-11-17-3435b: the secret of the identity that approves a pull request the host requires a review for, with
    /// respect_branch_protection: true. A host refuses the author's own approval (always on GitHub, by setting on Azure
    /// DevOps and GitLab), and the author is the repo's own token. Null: approve as the repo's token where allowed.
    /// </summary>
    public string? ApprovalAuth { get; set; }

    /// <summary>
    /// 2026-11-17-3435c: with on_ci_green, how many rework runs in a row a red CI may start before the ticket is handed
    /// back with the evidence. Three gives a flaky suite and a follow-up fix room while a fix that cannot converge stops
    /// spending; a person's answer resets the count. 2026-11-24-6811c: also how many repair passes a refresh-base run's
    /// master may make on a stage the merged base turned red — the same kind of fix, bounded the same way.
    /// </summary>
    public int CiFixAttempts { get; set; } = 3;

    /// <summary>
    /// 2026-11-24-f9c5a: this policy for a ticket pull request into its series branch — <see cref="SeriesWhen"/> as the mode
    /// and past protection: a series branch carries no host policy, so arming auto-complete there would merge at once with CI
    /// still running; agent-smith merges it itself once the mode allows. The original is left unchanged.
    /// </summary>
    public MergePolicyConfig IntoSeriesBranch()
    {
        var copy = (MergePolicyConfig)MemberwiseClone();
        copy.When = SeriesWhen;
        copy.RespectBranchProtection = false;
        return copy;
    }

    /// <summary>2026-11-24-f9c5c: the mode that decides a pull request into <paramref name="target"/> — series_when for a series
    /// branch, when for every other target (an unknown one included).</summary>
    public MergeWhen WhenInto(string? target) => Providers.WorkBranches.IsSeries(target) ? SeriesWhen : When;

    /// <summary>2026-11-24-f9c5c: whether either mode waits for CI, so the CI loop has anything to act on.</summary>
    public bool ActsOnCi => When == MergeWhen.OnCiGreen || SeriesWhen == MergeWhen.OnCiGreen;

    /// <summary>The protection mode a merge asks the provider for.</summary>
    public BranchProtection Protection => RespectBranchProtection ? BranchProtection.Respect : BranchProtection.Bypass;
}
