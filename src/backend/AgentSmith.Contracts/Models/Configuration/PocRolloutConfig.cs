namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-23-c81ec: how a PoC run follows its rollout on Argo CD, from <c>pocs.rollout</c>. Absent values read their
/// defaults; the load and the settings store refuse what <see cref="Problems"/> names, never clamping it.
/// </summary>
public sealed class PocRolloutConfig
{
    public const string Key = "pocs.rollout";
    public const string DeadlineSecondsKey = Key + ".deadline_seconds";
    public const string EndMarginSecondsKey = Key + ".end_margin_seconds";
    public const string CommitAllowanceSecondsKey = Key + ".commit_allowance_seconds";

    /// <summary>How often the follow reads the Application and the live Deployment.</summary>
    public int PollSeconds { get; set; } = 10;

    /// <summary>The longest one follow waits for the live Deployment to settle.</summary>
    public int DeadlineSeconds { get; set; } = 600;

    /// <summary>How long before the run's wall-time cancel the follow gives up, to write its failure first.</summary>
    public int EndMarginSeconds { get; set; } = 60;

    /// <summary>What sealing and the GitOps commit with its re-plans may take between the build and the follow.</summary>
    public int CommitAllowanceSeconds { get; set; } = 120;

    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        if (PollSeconds is < 1 or > 60)
            problems.Add($"{Key}.poll_seconds is {PollSeconds}; it must be 1 to 60, because each poll reads Argo CD twice and "
                + "a reading older than a minute shows the run page a rollout that already moved on");
        if (DeadlineSeconds < 60)
            problems.Add($"{DeadlineSecondsKey} is {DeadlineSeconds}; it must be 60 or more, because the git generator, the "
                + "sync and a pod's readiness take at least that long, and a shorter deadline fails healthy rollouts");
        if (EndMarginSeconds < 30)
            problems.Add($"{EndMarginSecondsKey} is {EndMarginSeconds}; it must be 30 or more, because the step must write "
                + "its failure and the PoC state before the wall-time cancel and its 30 s kill grace land");
        if (CommitAllowanceSeconds < 0)
            problems.Add($"{CommitAllowanceSecondsKey} is {CommitAllowanceSeconds}; it must be 0 or more, because sealing "
                + "and the GitOps commit with its re-plans run between the build and the follow");
        return problems;
    }
}
