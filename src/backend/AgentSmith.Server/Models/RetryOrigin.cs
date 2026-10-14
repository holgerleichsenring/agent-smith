namespace AgentSmith.Server.Models;

/// <summary>
/// Where a Retry was asked for. The Retry button retries unchanged only after an infrastructure end; a person
/// asking in the ticket's conversation is the act itself — a fixed precondition is a change outside the ticket —
/// so that origin admits any failed, cancelled or shortfall end and refuses only a delivered success.
/// </summary>
public enum RetryOrigin
{
    Button,
    Conversation,
}
