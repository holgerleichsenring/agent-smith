namespace AgentSmith.Contracts.Providers;

/// <summary>
/// p0490/p0501: what happened when a provider was asked to finish an already-opened
/// pull request. A refusal is a normal answer, not a failure — branch policies,
/// required reviewers and required builds all legitimately decline a completion,
/// and the pull request then stays open with <see cref="Reason"/> saying why.
/// </summary>
public sealed record PullRequestCompletion(PullRequestCompletionOutcome Outcome, string? Reason)
{
    /// <summary>The pull request is merged / completed on the platform.</summary>
    public static PullRequestCompletion Merged() => new(PullRequestCompletionOutcome.Merged, null);

    /// <summary>
    /// p0501: the platform accepted the instruction to finish the pull request but has
    /// not merged yet — it will, on its own, once the policy it is waiting on is
    /// satisfied. Nobody has to come back to it.
    /// </summary>
    public static PullRequestCompletion Armed(string reason) =>
        new(PullRequestCompletionOutcome.Armed, reason);

    /// <summary>The platform declined the completion, and said why.</summary>
    public static PullRequestCompletion Refused(string reason) =>
        new(PullRequestCompletionOutcome.Refused, reason);

    /// <summary>The pull request will reach the default branch without further help.</summary>
    public bool Settled => Outcome is PullRequestCompletionOutcome.Merged
        or PullRequestCompletionOutcome.Armed;
}

/// <summary>
/// p0501: the three answers a completion attempt can produce. Merged and Refused were
/// enough while completion meant an immediate merge; arming auto-complete adds an
/// outcome that is neither — the work is done and the pull request is not yet merged.
/// The surfaces act on all three differently, which is what earns the third member:
/// merged is finished, armed is waiting on a build nobody has to watch, refused is
/// still open and wants a human.
/// </summary>
public enum PullRequestCompletionOutcome
{
    /// <summary>The platform merged it.</summary>
    Merged,

    /// <summary>Auto-complete is set; the platform will merge it when its policy passes.</summary>
    Armed,

    /// <summary>The platform declined; the pull request stays open.</summary>
    Refused,
}
