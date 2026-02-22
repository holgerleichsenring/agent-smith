namespace AgentSmith.Domain.Models;

/// <summary>
/// Result of a command handler execution. Use static factories to create instances.
/// </summary>
public sealed record CommandResult
{
    public bool IsSuccess { get; }
    public string Message { get; }
    public Exception? Exception { get; }

    /// <summary>Pipeline step where failure occurred (1-based). 0 if unknown.</summary>
    public int FailedStep { get; init; }

    /// <summary>Total pipeline steps. 0 if unknown.</summary>
    public int TotalSteps { get; init; }

    /// <summary>Human-readable label of the step that failed.</summary>
    public string StepName { get; init; } = string.Empty;

    private CommandResult(bool success, string message, Exception? exception = null)
    {
        Message = message;
        IsSuccess = success;
        Exception = exception;
    }

    public static CommandResult Ok(string message) => new(true, message);

    public static CommandResult Fail(string message, Exception? exception = null)
    {
        return new(false, message, exception);
    }
}
