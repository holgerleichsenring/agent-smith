namespace AgentSmith.Contracts.Providers;

/// <summary>
/// p0490: what happened when a provider was asked to finish an already-opened
/// pull request. A refusal is a normal answer, not a failure — branch policies,
/// required reviewers and required builds all legitimately decline a completion,
/// and the pull request then stays open with <see cref="Reason"/> saying why.
/// </summary>
public sealed record PullRequestCompletion(bool Completed, string? Reason)
{
    /// <summary>The pull request is merged / completed on the platform.</summary>
    public static PullRequestCompletion Merged() => new(true, null);

    /// <summary>The platform declined the completion, and said why.</summary>
    public static PullRequestCompletion Refused(string reason) => new(false, reason);
}
