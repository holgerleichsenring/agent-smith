namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-17-023fa: the project's <c>merge:</c> block. 2026-11-25-9992i: it holds how the host's CI is read only; when a
/// pull request merges is the project's <see cref="ResolvedProject.Review"/>, and the host's branch protection is a limit
/// of the environment, never a setting.
/// </summary>
public sealed class MergePolicyConfig
{
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
    /// 2026-11-17-3435b: how often the open pull requests' CI is read. A minute keeps a green head merged within a minute
    /// and costs a few host calls per pull request per minute.
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
    /// 2026-11-17-3435c: with review: none, how many rework runs in a row a red CI may start before the ticket is handed
    /// back with the evidence. Three gives a flaky suite and a follow-up fix room while a fix that cannot converge stops
    /// spending; a person's answer resets the count. 2026-11-24-6811c: also how many repair passes a refresh-base run's
    /// master may make on a stage the merged base turned red — the same kind of fix, bounded the same way.
    /// </summary>
    public int CiFixAttempts { get; set; } = 3;
}
