namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59n: what a Retry did, one kind per answer the route gives.</summary>
public enum RunRetryResultKind
{
    UnknownRun,
    Invalid,
    Refused,
    Unavailable,
    Requested,
}

/// <summary>2026-10-13-9a59n: the answer to a Retry — the refusal body for a 409, the sentence otherwise.</summary>
public sealed record RunRetryResult(RunRetryResultKind Kind, string? Text = null, RetryRefusalView? Refusal = null,
    string? TicketId = null)
{
    public static readonly RunRetryResult UnknownRun = new(RunRetryResultKind.UnknownRun);

    public static RunRetryResult Invalid(string text) => new(RunRetryResultKind.Invalid, text);

    public static RunRetryResult Refused(RetryRefusalView refusal) => new(RunRetryResultKind.Refused, refusal.Text, refusal);

    public static RunRetryResult Unavailable(string text) => new(RunRetryResultKind.Unavailable, text);

    public static RunRetryResult Requested(string ticketId) => new(RunRetryResultKind.Requested, TicketId: ticketId);
}
