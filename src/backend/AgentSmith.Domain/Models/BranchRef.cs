namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-10-26-cb25e: a git ref as a branch name. Platforms answer with the wire form refs/heads/{branch}; callers
/// compare and show the short name. Only ONE LEADING prefix is the wire form — a branch may itself contain the text
/// refs/heads/, and the match is ordinal, as git's is. Not a <see cref="BranchName"/> factory: an empty answer stays
/// empty, and what an empty or missing branch means is the caller's call.
/// </summary>
public static class BranchRef
{
    public const string HeadsPrefix = "refs/heads/";

    public static string? ShortName(string? reference) =>
        reference?.StartsWith(HeadsPrefix, StringComparison.Ordinal) == true ? reference[HeadsPrefix.Length..] : reference;
}
