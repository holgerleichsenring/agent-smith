namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Discriminated union of the /spec chat commands (mirrors the ChatIntent
/// union style).
/// </summary>
public abstract record SpecCommand;

/// <summary>"/spec" or "/spec &lt;project&gt;" — open a session on this thread.</summary>
public sealed record SpecOpenCommand(string? Project) : SpecCommand;

/// <summary>"/spec list" — list open sessions.</summary>
public sealed record SpecListCommand : SpecCommand;

/// <summary>"/spec resume &lt;id&gt;" — continue a session in this thread.</summary>
public sealed record SpecResumeCommand(string SessionId) : SpecCommand;

/// <summary>"/spec new" — fork: close the thread's session, start fresh.</summary>
public sealed record SpecNewCommand(string? Project) : SpecCommand;
