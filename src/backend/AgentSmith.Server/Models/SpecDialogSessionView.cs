namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-15-cb3e: the conversation bound to one dialog id — what it is grounded in and
/// what has been said so far. The transcript is served because the dialog id survives a
/// reload: a page that kept the id and lost the conversation would look like a session
/// that had never happened.
/// </summary>
public sealed record SpecDialogSessionView(
    string SessionId,
    SpecDialogProjectView Scope,
    IReadOnlyList<SpecDialogTurnView> Transcript,
    DateTimeOffset LastActivityAt);
