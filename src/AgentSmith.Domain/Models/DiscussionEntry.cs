namespace AgentSmith.Domain.Models;

/// <summary>
/// A single entry in the multi-role plan discussion log.
/// </summary>
public sealed record DiscussionEntry(
    string RoleName,
    string DisplayName,
    string Emoji,
    int Round,
    string Content);
