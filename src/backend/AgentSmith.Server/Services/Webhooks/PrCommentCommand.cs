using AgentSmith.Contracts.Webhooks;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// A PR/MR comment as a host's handler read it from the payload: its text, its author, and
/// how the pull request is named in logs (<see cref="PrLabel"/>) and in the trigger input
/// (<see cref="PrReference"/>).
/// </summary>
public sealed record PrCommentCommand(
    string Body,
    PrCommentAuthor Author,
    string PrLabel,
    string PrReference);
