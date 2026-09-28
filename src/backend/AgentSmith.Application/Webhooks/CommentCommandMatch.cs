using AgentSmith.Contracts.Webhooks;

namespace AgentSmith.Application.Webhooks;

/// <summary>
/// The structural reading of a PR/MR comment: no command, a help request, or a command
/// whose <see cref="Tail"/> still has to be resolved to a pipeline.
/// </summary>
public sealed record CommentCommandMatch(CommentIntentType Type, string? Tail = null)
{
    public static CommentCommandMatch None { get; } = new(CommentIntentType.Unknown);

    public static CommentCommandMatch Help { get; } = new(CommentIntentType.Help);
}
