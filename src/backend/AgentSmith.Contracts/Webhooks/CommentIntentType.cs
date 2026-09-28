namespace AgentSmith.Contracts.Webhooks;

/// <summary>
/// Classifies the intent of a PR/MR comment command.
/// </summary>
public enum CommentIntentType
{
    NewJob,
    Help,
    Unknown,
}
