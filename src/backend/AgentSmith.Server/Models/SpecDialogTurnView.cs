namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-15-cb3e: one turn of the durable transcript, as the surface renders it. The
/// role is the wire's own word ("user" / "assistant") rather than a number, so a reader of
/// the payload can tell who said what.
/// </summary>
public sealed record SpecDialogTurnView(string Role, string Text, DateTimeOffset At);
