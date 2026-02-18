namespace AgentSmith.Domain.ValueObjects;

/// <summary>
/// Result of a command handler execution. Use static factories to create instances.
/// </summary>
public sealed record CommandResult
{
    public bool Success { get; }
    public string Message { get; }
    public Exception? Exception { get; }

    private CommandResult(bool success, string message, Exception? exception = null)
    {
        Message = message;
        Success = success;
        Exception = exception;
    }

    public static CommandResult Ok(string message) => new(true, message);

    public static CommandResult Fail(string message, Exception? exception = null)
    {
        return new(false, message, exception);
    }
}
