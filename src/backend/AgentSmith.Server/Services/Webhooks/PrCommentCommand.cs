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
    string PrNumber)
{
    /// <summary>The pull request's head branch — a ticket branch makes the command a rework act.
    /// Null when the host's delivery does not carry it and it was not read.</summary>
    public string? HeadRef { get; init; }

    /// <summary>True when the head branch lives in the pull request's own repository, not a fork.</summary>
    public bool SameRepository { get; init; }

    /// <summary>The pull request's URL in the shape its provider parses — what a rework nudge names.</summary>
    public string? PrUrl { get; init; }
}
