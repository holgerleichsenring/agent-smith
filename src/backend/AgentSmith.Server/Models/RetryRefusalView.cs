namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59n: why Retry did not start an attempt — every 409 of the route has this one body. The
/// dashboard branches on <see cref="Reason"/> (a <see cref="RetryRefusals"/> code) and shows
/// <see cref="Text"/> unchanged, beside the ticket's conversation.
/// </summary>
public sealed record RetryRefusalView(string Reason, string Text, string Project, string TicketId);

/// <summary>2026-10-13-9a59n: the stable reason codes of a refused Retry.</summary>
public static class RetryRefusals
{
    public const string NotLatest = "not-latest";
    public const string HandedBack = "handed-back";
    public const string NotFinished = "not-finished";
    public const string NotInfrastructure = "not-infrastructure";
    public const string ClosedForWork = "closed-for-work";
    public const string Starting = "starting";
    public const string Delivered = "delivered";

    public const string HandedBackText = "a hand-back is answered in the ticket conversation";

    public const string HandedBackConversationText =
        "the latest attempt handed the ticket back and waits — say what should change; a recorded change answers the hand-back";

    public const string DeliveredText =
        "the latest attempt delivered its work — say what should change before another attempt starts";

    public const string StartingText = "an attempt is already starting on this ticket";

    public const string NotInfrastructureText =
        "only an infrastructure end retries unchanged — say what should change in the ticket conversation";

    public const string ClosedForWorkText =
        "the ticket was closed in its tracker — approving a change in the ticket conversation files a follow-up";
}
