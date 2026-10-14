namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-10-13-9a59b: a pull request record's state. A host answers open, merged or closed;
/// unsupported and unread are what the record says when no host answer exists.
/// </summary>
public static class PrReviewStates
{
    public const string Open = "open";
    public const string Merged = "merged";
    public const string Closed = "closed";

    /// <summary>The repository's provider reads no review (a local repository).</summary>
    public const string Unsupported = "unsupported";

    /// <summary>The host failed the first read; nothing was ever read.</summary>
    public const string Unread = "unread";

    public static bool IsEnded(string state) => state is Merged or Closed;
}
