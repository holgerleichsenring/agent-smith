namespace AgentSmith.Contracts.Webhooks;

/// <summary>
/// Request to start a pipeline job triggered by a PR/MR comment.
/// </summary>
public sealed record PrJobRequest(
    string Pipeline,
    string Platform,
    string RepoFullName,
    string PrIdentifier,
    string? Arguments,
    string RequestedBy,
    string ChannelId);
