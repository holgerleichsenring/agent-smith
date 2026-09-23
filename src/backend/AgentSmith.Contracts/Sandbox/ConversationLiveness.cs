namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: what a reaper needs to know about the design conversation a
/// sandbox label names — whether it is still open, when it was last active, and the
/// project whose hold window governs it. Read from the session row, which is the
/// durable record every turn writes; a design turn holds no run lease at all, so the
/// lease is not available here even if it were wanted.
/// </summary>
public sealed record ConversationLiveness(
    string ConversationId,
    string Project,
    bool IsOpen,
    DateTimeOffset LastActivityAt);
