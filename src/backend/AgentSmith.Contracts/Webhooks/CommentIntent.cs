namespace AgentSmith.Contracts.Webhooks;

/// <summary>
/// Parsed intent from a PR/MR comment containing an agent command.
/// </summary>
public sealed record CommentIntent(
    CommentIntentType Type,
    string Platform,
    string RepoFullName,
    string PrIdentifier,
    string CommentId,
    string AuthorLogin,
    string? Pipeline,
    string? RawArguments,
    string? DialogueComment,
    string CommentBody);
