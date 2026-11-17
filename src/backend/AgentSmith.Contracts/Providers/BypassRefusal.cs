namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-17-023fa: the one sentence a provider answers when it could not merge past a host's protection — what
/// held, where, why, what to grant, and the key that turns the bypass off. A refusal, never a silent fallback.
/// </summary>
public static class BypassRefusal
{
    public const string Key = "respect_branch_protection";

    public static PullRequestCompletion Of(string policy, string provider, string reason, string permission) =>
        PullRequestCompletion.Refused(
            $"cannot bypass {policy} on {provider}: {reason}; grant {permission} or set {Key}: true");
}
