namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Structured error returned by <see cref="PathReadGuard"/> /
/// <see cref="PathWriteGuard"/>. The runtime translates it into a tool-result string
/// that flows back into the LLM loop instead of throwing.
/// </summary>
public sealed record GuardError
{
    public required GuardErrorKind Kind { get; init; }
    public required string Path { get; init; }
    public required string Message { get; init; }
}
