namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// The spec-dialog command a chat message can be. One case, because 2026-09-22-2a86 removed
/// the three that only the parser ever built — the list, the resume and the fork — once the
/// dashboard reached the resume by a route of its own.
/// </summary>
public abstract record SpecCommand;

/// <summary>
/// "/spec" or "/spec &lt;project&gt;" — open a session on this thread. Still a type rather
/// than a bare call because <see cref="SpecDialogConversationResolver"/> CONSTRUCTS it for a
/// page that already knows its project: it is not a spelling anybody has to type.
/// </summary>
public sealed record SpecOpenCommand(string? Project) : SpecCommand;
