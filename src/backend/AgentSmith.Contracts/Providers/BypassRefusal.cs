namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-17-023fa: the one sentence a provider answers when it could not merge past a host's protection — what
/// held, where, why, and what to grant. A refusal, never a silent fallback. 2026-11-25-9992i: no setting turns the bypass
/// off any more; the alternative it names is a merge the host's rules decide (merge_pull_request how auto).
/// </summary>
public static class BypassRefusal
{
    public const string Alternative = "or merge with how auto so the host's rules decide";

    public static PullRequestCompletion Of(string policy, string provider, string reason, string permission) =>
        PullRequestCompletion.Refused(
            $"cannot bypass {policy} on {provider}: {reason}; grant {permission}, {Alternative}");
}
