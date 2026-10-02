namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-10-02-5f89g: what a clone, fetch or push authenticates with — the token behind the
/// repo's own auth secret, or nothing for a local working copy. The git credential helper
/// echoes it as <c>$GIT_TOKEN</c>; <see cref="None"/> sets no such variable.
/// </summary>
public sealed record GitCredential(string? Token)
{
    public static GitCredential None { get; } = new((string?)null);

    public bool HasToken => !string.IsNullOrEmpty(Token);
}
