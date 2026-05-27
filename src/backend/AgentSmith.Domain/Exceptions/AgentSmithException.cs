namespace AgentSmith.Domain.Exceptions;

/// <summary>
/// Base exception for all domain-specific errors.
/// </summary>
public class AgentSmithException : Exception
{
    public AgentSmithException(string message)
        : base(message) { }

    public AgentSmithException(string message, Exception innerException)
        : base(message, innerException) { }
}
