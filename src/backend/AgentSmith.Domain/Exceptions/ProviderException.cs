namespace AgentSmith.Domain.Exceptions;

/// <summary>
/// Thrown when a provider encounters an error during execution.
/// </summary>
public sealed class ProviderException : AgentSmithException
{
    public string ProviderType { get; }

    public ProviderException(string providerType, string message, Exception? innerException = null)
        : base(message, innerException ?? new Exception(message))
    {
        ProviderType = providerType;
    }
}
