using AgentSmith.Contracts.Webhooks;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// A PR/MR comment as a host's handler read it from the payload: its text, its author, and
/// how the pull request is named in logs (<see cref="PrLabel"/>) and in the trigger input
/// (<see cref="PrReference"/>). 2026-10-08-e8b9e: <see cref="PrNumber"/> is the host's own number,
/// which the outcome comment is posted under.
/// </summary>
public sealed record PrCommentCommand(
    string Body,
    PrCommentAuthor Author,
    string PrLabel,
    string PrReference,
    string PrNumber);
