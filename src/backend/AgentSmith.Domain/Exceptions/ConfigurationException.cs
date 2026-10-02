namespace AgentSmith.Domain.Exceptions;

/// <summary>
/// Thrown when configuration is invalid or cannot be loaded.
/// </summary>
public class ConfigurationException : AgentSmithException
{
    public ConfigurationException(string message)
        : base(message) { }
}
