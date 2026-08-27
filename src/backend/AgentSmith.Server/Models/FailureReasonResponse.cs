namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-08-27-7098: what an unhandled failure says. ASP.NET's own 500 path writes an EMPTY
/// body, so a client that prefers a reason from the body has nothing to prefer and renders
/// the status code instead — "Could not start the initialization (HTTP 500)" told the
/// operator only that a number came back. This names what was actually wrong.
/// </summary>
public sealed record FailureReasonResponse(string Reason);
