namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-15-cb3e: one open session the caller may resume, as the surface lists it —
/// what "/spec list" answers in a chat channel.
/// </summary>
public sealed record SpecDialogSessionSummary(
    string SessionId, string Project, int Turns, DateTimeOffset LastActivityAt);
